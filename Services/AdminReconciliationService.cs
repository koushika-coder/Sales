using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services
{
    public interface IAdminReconciliationService
    {
        // All days (up to 30) that have data but no SummaryCommit — includes failed commits (diff > £5)
        Task<List<PendingReconciliationResponse>> GetAllPendingAsync();

        // Admin edits and submits a specific date's reconciliation
        Task<PendingReconciliationResponse> SubmitAsync(int adminId, AdminSubmitReconciliationRequest request);

        // All committed summaries — list for date picker
        Task<List<CommittedSummaryListItem>> GetAllCommittedAsync();

        // Full breakdown for a specific committed date
        Task<CommittedSummaryDetailResponse?> GetCommittedByDateAsync(DateOnly date);

        // Staff portal: yesterday's admin-submitted reconciliation
        Task<ReconciliationPortalResponse?> GetPortalReconciliationAsync(DateOnly? date = null);
    }

    public class AdminReconciliationService : IAdminReconciliationService
    {
        private readonly SalesDbContext _db;
        private readonly IEmailService _email;

        public AdminReconciliationService(SalesDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        // ── Pending: all un-committed days (up to 30 days back) ─────────────

        public async Task<List<PendingReconciliationResponse>> GetAllPendingAsync()
        {
            var today     = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);
            var lookbackStart = today.AddDays(-30);

            // Dates already resolved: staff committed or admin reconciled
            var committedDates = (await _db.SummaryCommits
                .Where(c => c.Date >= lookbackStart && c.Date <= today)
                .Select(c => c.Date)
                .ToListAsync())
                .ToHashSet();

            var adminReconciledDates = (await _db.AdminReconciliations
                .Where(r => r.Status == "submitted" && r.Date >= lookbackStart && r.Date <= today)
                .Select(r => r.Date)
                .ToListAsync())
                .ToHashSet();

            var result = new List<PendingReconciliationResponse>();

            // Start from yesterday — today is still in progress for staff
            for (var date = yesterday; date >= lookbackStart; date = date.AddDays(-1))
            {
                if (committedDates.Contains(date)) continue;
                if (adminReconciledDates.Contains(date)) continue;

                var rangeStart = date.ToDateTime(TimeOnly.MinValue);
                var rangeEnd   = rangeStart.AddDays(1);

                // Only include this date if at least one piece of data was entered
                var hasData =
                    await _db.CreditCardBanking.AnyAsync(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd) ||
                    await _db.SafeDrops.AnyAsync(s => s.Date == date) ||
                    await _db.Deductions.AnyAsync(d => d.CreatedAt >= rangeStart && d.CreatedAt < rangeEnd) ||
                    await _db.Lotteries.AnyAsync(l => l.CreatedDate >= rangeStart && l.CreatedDate < rangeEnd) ||
                    await _db.Paypoints.AnyAsync(p => p.CreatedDate >= rangeStart && p.CreatedDate < rangeEnd);

                if (!hasData) continue;

                result.Add(await BuildPendingItemAsync(date, rangeStart, rangeEnd));
            }

            return result;
        }

        private async Task<PendingReconciliationResponse> BuildPendingItemAsync(
            DateOnly date, DateTime rangeStart, DateTime rangeEnd)
        {
            var manualCard = await _db.CreditCardBanking
                .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd)
                .SumAsync(c => (decimal?)c.ManualCardAmount) ?? 0m;

            var cardAmount = await _db.CreditCardBanking
                .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd)
                .SumAsync(c => (decimal?)c.CardAmount) ?? 0m;

            var safeDrop  = await _db.SafeDrops.FirstOrDefaultAsync(s => s.Date == date);
            var prevClose = await _db.SafeDrops
                .Where(s => s.Date == date.AddDays(-1))
                .Select(s => (decimal?)s.SafeDropAmount)
                .FirstOrDefaultAsync() ?? 0m;
            var lastSafe      = safeDrop?.LastSafe ?? prevClose;
            var safeDropAmount = safeDrop?.SafeDropAmount ?? 0m;

            var deduction = await _db.Deductions
                .Where(d => d.CreatedAt >= rangeStart && d.CreatedAt < rangeEnd)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            var ilCount = await _db.LotteryInventory
                .Where(li => li.InventoryDate >= rangeStart && li.InventoryDate < rangeEnd)
                .SumAsync(li => (int?)li.TotalSold) ?? 0;

            var ilSales = await _db.LotteryInventory
                .Where(li => li.InventoryDate >= rangeStart && li.InventoryDate < rangeEnd)
                .SumAsync(li => (decimal?)li.Sales) ?? 0m;

            var lottery  = await _db.Lotteries
                .Where(l => l.CreatedDate >= rangeStart && l.CreatedDate < rangeEnd)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            var paypoint = await _db.Paypoints
                .Where(p => p.CreatedDate >= rangeStart && p.CreatedDate < rangeEnd)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            var cash         = lastSafe + safeDropAmount;
            var summaryTotal = manualCard + cardAmount + cash;

            return new PendingReconciliationResponse
            {
                HasPending = true,
                Id = 0,
                Date = date,
                ManualCardAmount = manualCard,
                CardAmount = cardAmount,
                LastSafe = lastSafe,
                SafeDropAmount = safeDropAmount,
                Cashback = deduction?.Cashback ?? 0m,
                PaypointPayout = deduction?.PaypointPayout ?? 0m,
                InstantLotteryPayout = deduction?.InstantLotteryPayout ?? 0m,
                NewsVoucher = deduction?.NewsVoucher ?? 0m,
                DDPoint = deduction?.DDPoint ?? 0m,
                LotteryPayout = deduction?.LotteryPayout ?? 0m,
                InstantLotteryTotalCount = ilCount,
                InstantLotteryTotalSales = ilSales,
                LotteryValue = lottery?.LotteryValue ?? 0m,
                PaypointValue = paypoint?.PaypointValue ?? 0m,
                SummaryTotal = summaryTotal,
                ZReportTotal = 0m,   // requires Z-report email — admin fills this in on submit
                Difference = 0m,     // calculated by admin on submit
                CreatedAt = rangeStart,
            };
        }

        // ── Submit (admin finalises a specific date's reconciliation) ─────────

        public async Task<PendingReconciliationResponse> SubmitAsync(int adminId, AdminSubmitReconciliationRequest request)
        {
            var date = request.Date;

            var existing = await _db.AdminReconciliations.FirstOrDefaultAsync(r => r.Date == date);

            if (existing is not null && existing.Status == "submitted")
                throw new InvalidOperationException($"Reconciliation for {date} has already been submitted.");

            if (existing is null)
            {
                existing = new AdminReconciliation { Date = date, CreatedAt = DateTime.UtcNow };
                _db.AdminReconciliations.Add(existing);
            }

            existing.ManualCardAmount = request.ManualCardAmount;
            existing.CardAmount = request.CardAmount;
            existing.LastSafe = request.LastSafe;
            existing.SafeDropAmount = request.SafeDropAmount;
            existing.Cashback = request.Cashback;
            existing.PaypointPayout = request.PaypointPayout;
            existing.InstantLotteryPayout = request.InstantLotteryPayout;
            existing.NewsVoucher = request.NewsVoucher;
            existing.DDPoint = request.DDPoint;
            existing.LotteryPayout = request.LotteryPayout;
            existing.LotteryValue = request.LotteryValue;
            existing.PaypointValue = request.PaypointValue;
            existing.SummaryTotal = request.SummaryTotal;
            existing.ZReportTotal = request.ZReportTotal;
            existing.Difference = request.Difference;
            existing.AdminNotes = request.AdminNotes;
            existing.Status = "submitted";
            existing.SubmittedByAdminId = adminId;
            existing.SubmittedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _email.SendReconciliationSubmittedEmailAsync(request, existing.Date);

            return new PendingReconciliationResponse
            {
                HasPending = false,
                Id = existing.Id,
                Date = existing.Date,
                ManualCardAmount = existing.ManualCardAmount,
                CardAmount = existing.CardAmount,
                LastSafe = existing.LastSafe,
                SafeDropAmount = existing.SafeDropAmount,
                Cashback = existing.Cashback,
                PaypointPayout = existing.PaypointPayout,
                InstantLotteryPayout = existing.InstantLotteryPayout,
                NewsVoucher = existing.NewsVoucher,
                DDPoint = existing.DDPoint,
                LotteryPayout = existing.LotteryPayout,
                InstantLotteryTotalCount = 0,
                InstantLotteryTotalSales = existing.InstantLotteryTotalSales,
                LotteryValue = existing.LotteryValue,
                PaypointValue = existing.PaypointValue,
                SummaryTotal = existing.SummaryTotal,
                ZReportTotal = existing.ZReportTotal,
                Difference = existing.Difference,
                CreatedAt = existing.CreatedAt,
            };
        }

        // ── Committed history ────────────────────────────────────────────────

        public async Task<List<CommittedSummaryListItem>> GetAllCommittedAsync()
        {
            // Staff self-commits
            var staffItems = await _db.SummaryCommits
                .OrderByDescending(c => c.Date)
                .Select(c => new CommittedSummaryListItem
                {
                    Id           = c.Id,
                    Date         = c.Date,
                    SummaryTotal = c.SummaryTotal,
                    ZReportTotal = c.ZReportTotal,
                    Difference   = c.Difference,
                    CommittedAt  = c.CommittedAt,
                })
                .ToListAsync();

            var staffDates = staffItems.Select(x => x.Date).ToHashSet();

            // Admin-reconciled dates that were never self-committed by staff
            var adminItems = await _db.AdminReconciliations
                .Where(r => r.Status == "submitted" && !staffDates.Contains(r.Date))
                .OrderByDescending(r => r.Date)
                .Select(r => new CommittedSummaryListItem
                {
                    Id           = r.Id,
                    Date         = r.Date,
                    SummaryTotal = r.SummaryTotal,
                    ZReportTotal = r.ZReportTotal,
                    Difference   = r.Difference,
                    CommittedAt  = r.SubmittedAt ?? r.CreatedAt,
                })
                .ToListAsync();

            return staffItems
                .Concat(adminItems)
                .OrderByDescending(x => x.Date)
                .ToList();
        }

        public async Task<CommittedSummaryDetailResponse?> GetCommittedByDateAsync(DateOnly date)
        {
            var commit = await _db.SummaryCommits.FirstOrDefaultAsync(c => c.Date == date);

            // If no staff self-commit, fall back to admin reconciliation for this date
            if (commit is null)
            {
                var adminRec = await _db.AdminReconciliations
                    .FirstOrDefaultAsync(r => r.Date == date && r.Status == "submitted");

                if (adminRec is null) return null;

                return new CommittedSummaryDetailResponse
                {
                    CommitId                 = adminRec.Id,
                    Date                     = adminRec.Date,
                    ManualCardAmount         = adminRec.ManualCardAmount,
                    CardAmount               = adminRec.CardAmount,
                    LastSafe                 = adminRec.LastSafe,
                    SafeDropAmount           = adminRec.SafeDropAmount,
                    Cashback                 = adminRec.Cashback,
                    PaypointPayout           = adminRec.PaypointPayout,
                    InstantLotteryPayout     = adminRec.InstantLotteryPayout,
                    NewsVoucher              = adminRec.NewsVoucher,
                    DDPoint                  = adminRec.DDPoint,
                    LotteryPayout            = adminRec.LotteryPayout,
                    InstantLotteryTotalCount = adminRec.InstantLotteryTotalCount,
                    InstantLotteryTotalSales = adminRec.InstantLotteryTotalSales,
                    LotteryValue             = adminRec.LotteryValue,
                    PaypointValue            = adminRec.PaypointValue,
                    SummaryTotal             = adminRec.SummaryTotal,
                    ZReportTotal             = adminRec.ZReportTotal,
                    Difference               = adminRec.Difference,
                    CommittedAt              = adminRec.SubmittedAt ?? adminRec.CreatedAt,
                };
            }

            // If an admin has submitted a reconciliation for this date, use its totals
            // to patch any zeros stored at staff-commit time (e.g. due to Z-report bug).
            var adminPatch = await _db.AdminReconciliations
                .FirstOrDefaultAsync(r => r.Date == date && r.Status == "submitted");

            var rangeStart = date.ToDateTime(TimeOnly.MinValue);
            var rangeEnd   = rangeStart.AddDays(1);

            var manualCard = await _db.CreditCardBanking
                .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd)
                .SumAsync(c => (decimal?)c.ManualCardAmount) ?? 0m;

            var cardAmount = await _db.CreditCardBanking
                .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd)
                .SumAsync(c => (decimal?)c.CardAmount) ?? 0m;

            var safeDrop = await _db.SafeDrops.FirstOrDefaultAsync(s => s.Date == date);

            var deduction = await _db.Deductions
                .Where(d => d.CreatedAt >= rangeStart && d.CreatedAt < rangeEnd)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            var ilCount = await _db.LotteryInventory
                .Where(li => li.InventoryDate >= rangeStart && li.InventoryDate < rangeEnd)
                .SumAsync(li => (int?)li.TotalSold) ?? 0;

            var ilSales = await _db.LotteryInventory
                .Where(li => li.InventoryDate >= rangeStart && li.InventoryDate < rangeEnd)
                .SumAsync(li => (decimal?)li.Sales) ?? 0m;

            var lottery = await _db.Lotteries
                .Where(l => l.CreatedDate >= rangeStart && l.CreatedDate < rangeEnd)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            var paypoint = await _db.Paypoints
                .Where(p => p.CreatedDate >= rangeStart && p.CreatedDate < rangeEnd)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            return new CommittedSummaryDetailResponse
            {
                CommitId                 = commit.Id,
                Date                     = commit.Date,
                ManualCardAmount         = manualCard,
                CardAmount               = cardAmount,
                LastSafe                 = safeDrop?.LastSafe      ?? 0m,
                SafeDropAmount           = safeDrop?.SafeDropAmount ?? 0m,
                Cashback                 = deduction?.Cashback             ?? 0m,
                PaypointPayout           = deduction?.PaypointPayout       ?? 0m,
                InstantLotteryPayout     = deduction?.InstantLotteryPayout  ?? 0m,
                NewsVoucher              = deduction?.NewsVoucher           ?? 0m,
                DDPoint                  = deduction?.DDPoint               ?? 0m,
                LotteryPayout            = deduction?.LotteryPayout         ?? 0m,
                InstantLotteryTotalCount = ilCount,
                InstantLotteryTotalSales = ilSales,
                LotteryValue             = lottery?.LotteryValue   ?? 0m,
                PaypointValue            = paypoint?.PaypointValue ?? 0m,
                SummaryTotal             = adminPatch?.SummaryTotal ?? commit.SummaryTotal,
                ZReportTotal             = adminPatch?.ZReportTotal ?? commit.ZReportTotal,
                Difference               = adminPatch?.Difference   ?? commit.Difference,
                CommittedAt              = commit.CommittedAt,
            };
        }

        // ── Staff portal ─────────────────────────────────────────────────────

        public async Task<ReconciliationPortalResponse?> GetPortalReconciliationAsync(DateOnly? date = null)
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

            var record = await _db.AdminReconciliations
                .FirstOrDefaultAsync(r => r.Date == targetDate && r.Status == "submitted");

            if (record is null) return null;

            return new ReconciliationPortalResponse
            {
                HasReconciliation = true,
                Id = record.Id,
                Date = record.Date,
                ManualCardAmount = record.ManualCardAmount,
                CardAmount = record.CardAmount,
                LastSafe = record.LastSafe,
                SafeDropAmount = record.SafeDropAmount,
                Cashback = record.Cashback,
                PaypointPayout = record.PaypointPayout,
                InstantLotteryPayout = record.InstantLotteryPayout,
                NewsVoucher = record.NewsVoucher,
                DDPoint = record.DDPoint,
                LotteryPayout = record.LotteryPayout,
                InstantLotteryTotalCount = record.InstantLotteryTotalCount,
                InstantLotteryTotalSales = record.InstantLotteryTotalSales,
                LotteryValue = record.LotteryValue,
                PaypointValue = record.PaypointValue,
                SummaryTotal = record.SummaryTotal,
                ZReportTotal = record.ZReportTotal,
                Difference = record.Difference,
                AdminNotes = record.AdminNotes,
                SubmittedAt = record.SubmittedAt!.Value,
            };
        }
    }
}
