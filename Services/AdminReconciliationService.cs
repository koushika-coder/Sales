using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System.IO.Compression;
using System.Text;

namespace Sales.Services
{
    public interface IAdminReconciliationService
    {
        // All days (up to 30) that have data but no SummaryCommit — includes failed commits (diff > £5)
        Task<List<PendingReconciliationResponse>> GetAllPendingAsync();

        // Admin edits and submits a specific date's reconciliation
        Task<PendingReconciliationResponse> SubmitAsync(int adminId, AdminSubmitReconciliationRequest request);

        // All committed summaries — list for date picker
        Task<List<CommittedSummaryListItem>> GetAllCommittedAsync(DateOnly? fromDate = null, DateOnly? toDate = null);

        // Full breakdown for a specific committed date
        Task<CommittedSummaryDetailResponse?> GetCommittedByDateAsync(DateOnly date);

        // Staff portal: yesterday's admin-submitted reconciliation
        Task<ReconciliationPortalResponse?> GetPortalReconciliationAsync(DateOnly? date = null);

        // Download the Z-report bill (Gmail email) for one date as a PDF.
        // Null means no matching Z-report email was found for that date.
        Task<(string FileName, byte[] Bytes)?> DownloadZReportBillAsync(DateOnly date);

        // Download bills for a date range as a single ZIP (one PDF per date found).
        Task<(string FileName, byte[] Bytes)> DownloadZReportBillsRangeAsync(DateOnly fromDate, DateOnly toDate);
    }

    public class AdminReconciliationService : IAdminReconciliationService
    {
        private readonly SalesDbContext _db;
        private readonly IEmailService _email;
        private readonly IGmailService _gmail;

        public AdminReconciliationService(SalesDbContext db, IEmailService email, IGmailService gmail)
        {
            _db    = db;
            _email = email;
            _gmail = gmail;
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
            var includedDates = new HashSet<DateOnly>();

            // Dates with a failed commit attempt (difference > £5.00) are recorded as
            // "pending" rows the moment staff tries to commit, so they surface immediately —
            // even for today's active date, not just yesterday-and-earlier.
            var failedAttempts = await _db.AdminReconciliations
                .Where(r => r.Status == "pending" && r.Date >= lookbackStart && r.Date <= today)
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            foreach (var r in failedAttempts)
            {
                if (committedDates.Contains(r.Date) || adminReconciledDates.Contains(r.Date)) continue;

                result.Add(new PendingReconciliationResponse
                {
                    HasPending = true,
                    Id = r.Id,
                    Date = r.Date,
                    ManualCardAmount = r.ManualCardAmount,
                    CardAmount = r.CardAmount,
                    LastSafe = r.LastSafe,
                    SafeDropAmount = r.SafeDropAmount,
                    Cashback = r.Cashback,
                    PaypointPayout = r.PaypointPayout,
                    InstantLotteryPayout = r.InstantLotteryPayout,
                    NewsVoucher = r.NewsVoucher,
                    DDPoint = r.DDPoint,
                    LotteryPayout = r.LotteryPayout,
                    SupplierInvoicesTotal = r.SupplierInvoicesTotal,
                    InstantLotteryTotalCount = r.InstantLotteryTotalCount,
                    InstantLotteryTotalSales = r.InstantLotteryTotalSales,
                    LotteryValue = r.LotteryValue,
                    PaypointValue = r.PaypointValue,
                    SummaryTotal = r.SummaryTotal,
                    ZReportTotal = r.ZReportTotal,
                    Difference = r.Difference,
                    CreatedAt = r.CreatedAt,
                });
                includedDates.Add(r.Date);
            }

            // Start from yesterday — today is still in progress for staff (unless a failed
            // commit attempt already surfaced it above).
            for (var date = yesterday; date >= lookbackStart; date = date.AddDays(-1))
            {
                if (committedDates.Contains(date)) continue;
                if (adminReconciledDates.Contains(date)) continue;
                if (includedDates.Contains(date)) continue;

                var rangeStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
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

            return result.OrderByDescending(x => x.Date).ToList();
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

            var supplierInvoicesTotal = await _db.SupplierInvoices
                .Where(i => i.CreatedAt >= rangeStart && i.CreatedAt < rangeEnd)
                .SumAsync(i => (decimal?)i.Value) ?? 0m;

            var cash         = lastSafe + safeDropAmount;
            var summaryTotal = manualCard + cardAmount + cash + supplierInvoicesTotal;
            var zReportTotal = await _gmail.GetZReportTotalForDateAsync(date) ?? 0m;

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
                SupplierInvoicesTotal = supplierInvoicesTotal,
                InstantLotteryTotalCount = ilCount,
                InstantLotteryTotalSales = ilSales,
                LotteryValue = lottery?.LotteryValue ?? 0m,
                PaypointValue = paypoint?.PaypointValue ?? 0m,
                SummaryTotal = summaryTotal,
                ZReportTotal = zReportTotal,
                Difference = Math.Abs(summaryTotal - zReportTotal),
                CreatedAt = rangeStart,
            };
        }

        // ── Submit (admin finalises a specific date's reconciliation) ─────────

        public async Task<PendingReconciliationResponse> SubmitAsync(int adminId, AdminSubmitReconciliationRequest request)
        {
            var date = request.Date;

            var existing = await _db.AdminReconciliations.FirstOrDefaultAsync(r => r.Date == date);

            // Admins can revise a submitted reconciliation as many times as needed —
            // no lock after the first submit, unlike the staff commit flow.
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
            existing.SupplierInvoicesTotal = request.SupplierInvoicesTotal;
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

            try { await _email.SendReconciliationSubmittedEmailAsync(request, existing.Date); }
            catch { /* email failure must not block a successful submit — data is already saved */ }

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
                SupplierInvoicesTotal = existing.SupplierInvoicesTotal,
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

        public async Task<List<CommittedSummaryListItem>> GetAllCommittedAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
        {
            // Staff self-commits
            var staffQuery = _db.SummaryCommits.AsQueryable();
            if (fromDate is not null)
                staffQuery = staffQuery.Where(c => c.Date >= fromDate.Value);
            if (toDate is not null)
                staffQuery = staffQuery.Where(c => c.Date <= toDate.Value);

            var staffItems = await staffQuery
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
            var adminQuery = _db.AdminReconciliations
                .Where(r => r.Status == "submitted" && !staffDates.Contains(r.Date));
            if (fromDate is not null)
                adminQuery = adminQuery.Where(r => r.Date >= fromDate.Value);
            if (toDate is not null)
                adminQuery = adminQuery.Where(r => r.Date <= toDate.Value);

            var adminItems = await adminQuery
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
                    SupplierInvoicesTotal    = adminRec.SupplierInvoicesTotal,
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

            var rangeStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
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

            var liveSupplierInvoicesTotal = await _db.SupplierInvoices
                .Where(i => i.CreatedAt >= rangeStart && i.CreatedAt < rangeEnd)
                .SumAsync(i => (decimal?)i.Value) ?? 0m;
            var supplierInvoicesTotal = adminPatch?.SupplierInvoicesTotal ?? liveSupplierInvoicesTotal;

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
                SupplierInvoicesTotal    = supplierInvoicesTotal,
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
                SupplierInvoicesTotal = record.SupplierInvoicesTotal,
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

        // ── Download bill (Z-report Gmail email → PDF) ──────────────────────

        public async Task<(string FileName, byte[] Bytes)?> DownloadZReportBillAsync(DateOnly date)
        {
            var email = await FindZReportEmailForDateAsync(date);
            if (email is null) return null;

            var bytes = BuildBillPdf(date, email);
            return ($"zreport-bill-{date:yyyy-MM-dd}.pdf", bytes);
        }

        public async Task<(string FileName, byte[] Bytes)> DownloadZReportBillsRangeAsync(DateOnly fromDate, DateOnly toDate)
        {
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var date = fromDate; date <= toDate; date = date.AddDays(1))
                {
                    var email = await FindZReportEmailForDateAsync(date);
                    if (email is null) continue; // no bill for this date — skip, don't fail the batch

                    var bytes = BuildBillPdf(date, email);
                    var entry = archive.CreateEntry($"{date:yyyy-MM-dd}.pdf", CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(bytes);
                }
            }

            var fileName = $"zreport-bills-{fromDate:yyyy-MM-dd}-to-{toDate:yyyy-MM-dd}.zip";
            return (fileName, zipStream.ToArray());
        }

        // Finds the Z-report email for a date regardless of commit status — an admin
        // downloading a historical bill should be able to get it whether or not the
        // day has since been committed (unlike the staff-facing Z-Report Viewer).
        private async Task<GmailMessageResponse?> FindZReportEmailForDateAsync(DateOnly date)
        {
            var emails = await _gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z-Report",
                MaxResults     = 50,
            });

            return emails.FirstOrDefault(e =>
                !e.Body.TrimStart().StartsWith('<') &&
                e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) &&
                ParseEmailReceivedDate(e.Date) == date);
        }

        // Parses the RFC 2822 "Date" header of an email and returns the date in the
        // timezone the sender used — this is the date the user sees in their inbox.
        private static DateOnly? ParseEmailReceivedDate(string emailDate)
        {
            if (string.IsNullOrWhiteSpace(emailDate)) return null;
            if (DateTimeOffset.TryParse(
                    emailDate,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dto))
                return DateOnly.FromDateTime(dto.DateTime);
            return null;
        }

        // Builds a simple multi-page PDF (no external library, matches the hand-rolled
        // approach ReportService uses) from the email's subject + plain-text body.
        private static byte[] BuildBillPdf(DateOnly date, GmailMessageResponse email)
        {
            const int maxCharsPerLine = 100;

            var lines = new List<string>
            {
                $"Z-Report Bill - {date:yyyy-MM-dd}",
                $"Subject: {email.Subject}",
                string.Empty,
            };

            foreach (var rawLine in email.Body.Replace("\r\n", "\n").Split('\n'))
            {
                lines.AddRange(WrapLine(rawLine, maxCharsPerLine));
            }

            return BuildMultiPagePdf(lines);
        }

        private static List<string> WrapLine(string line, int maxChars)
        {
            if (string.IsNullOrEmpty(line) || line.Length <= maxChars)
                return new List<string> { line };

            var wrapped = new List<string>();
            for (var i = 0; i < line.Length; i += maxChars)
                wrapped.Add(line.Substring(i, Math.Min(maxChars, line.Length - i)));
            return wrapped;
        }

        private static byte[] BuildMultiPagePdf(IReadOnlyList<string> lines)
        {
            const int linesPerPage = 60;
            const int startY = 760;
            const int lineHeight = 12;

            var pageContents = new List<string>();
            for (var i = 0; i < lines.Count; i += linesPerPage)
            {
                var y = startY;
                var sb = new StringBuilder();
                foreach (var line in lines.Skip(i).Take(linesPerPage))
                {
                    sb.AppendLine($"BT /F1 10 Tf 72 {y} Td ({EscapePdfText(line)}) Tj ET");
                    y -= lineHeight;
                }
                pageContents.Add(sb.ToString());
            }
            if (pageContents.Count == 0) pageContents.Add(string.Empty);

            var pageCount = pageContents.Count;
            const int pageObjStart = 3;
            var contentObjStart = pageObjStart + pageCount;
            var fontObjNum = contentObjStart + pageCount;

            var kids = string.Join(" ", Enumerable.Range(pageObjStart, pageCount).Select(n => $"{n} 0 R"));

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>",
            };

            for (var p = 0; p < pageCount; p++)
            {
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents {contentObjStart + p} 0 R /Resources << /Font << /F1 {fontObjNum} 0 R >> >> >>");
            }

            for (var p = 0; p < pageCount; p++)
            {
                var contentBytes = Encoding.ASCII.GetBytes(pageContents[p]);
                objects.Add($"<< /Length {contentBytes.Length} >>\nstream\n{pageContents[p]}\nendstream");
            }

            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var pdf = new StringBuilder();
            pdf.AppendLine("%PDF-1.4");
            var offsets = new List<int>();

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.AppendLine($"{i + 1} 0 obj");
                pdf.AppendLine(objects[i]);
                pdf.AppendLine("endobj");
            }

            var xrefPosition = pdf.Length;
            pdf.AppendLine("xref");
            pdf.AppendLine($"0 {objects.Count + 1}");
            pdf.AppendLine("0000000000 65535 f ");

            foreach (var offset in offsets)
            {
                pdf.AppendLine(offset.ToString("D10") + " 00000 n ");
            }

            pdf.AppendLine("trailer");
            pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
            pdf.AppendLine($"startxref\n{xrefPosition}\n%%EOF");

            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        private static string EscapePdfText(string value) =>
            value.Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
    }
}
