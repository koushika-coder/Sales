using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using Sales.Models.Sales.Models;

namespace Sales.Services
{
    public interface ISummaryService
    {
        Task<SummaryResponse> GetTodayAsync(int userId);
        Task<SummaryResponse> UpdateTodayAsync(int userId, SummaryUpdateRequest request);
        Task<ZReportComparisonResponse> GetZReportComparisonAsync(int userId);
        Task<SummaryCommitResponse> CommitTodayAsync(int userId, SummaryCommitRequest request);
    }

    public class SummaryService : ISummaryService
    {
        private readonly SalesDbContext _db;
        private readonly IGmailService _gmail;
        private readonly IEmailService _email;

        public SummaryService(SalesDbContext db, IGmailService gmail, IEmailService email)
        {
            _db    = db;
            _gmail = gmail;
            _email = email;
        }

        public async Task<SummaryResponse> GetTodayAsync(int userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            var creditCardEntries = await _db.CreditCardBanking
                .Where(c => c.UserId == userId
                         && c.CreatedDate >= todayStart
                         && c.CreatedDate < todayEnd)
                .OrderBy(c => c.CreatedDate)
                .Select(c => new CreditCardSummaryEntry
                {
                    Id = c.Id,
                    ManualCardAmount = c.ManualCardAmount,
                    CardAmount = c.CardAmount,
                    CreatedDate = c.CreatedDate,
                })
                .ToListAsync();

            var safeDrop = await _db.SafeDrops
                .FirstOrDefaultAsync(s => s.Date == today);

            var deduction = await _db.Deductions
                .Where(d => d.UserId == userId
                         && d.CreatedAt >= todayStart
                         && d.CreatedAt < todayEnd)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            var instantLotteryTotalSales = await _db.LotteryInventory
                .Where(li => li.UserId == userId
                          && li.InventoryDate >= todayStart
                          && li.InventoryDate < todayEnd)
                .SumAsync(li => (decimal?)li.Sales) ?? 0m;

            var lottery = await _db.Lotteries
                .Where(l => l.UserId == userId
                         && l.CreatedDate >= todayStart
                         && l.CreatedDate < todayEnd)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            var paypoint = await _db.Paypoints
                .Where(p => p.UserId == userId
                         && p.CreatedDate >= todayStart
                         && p.CreatedDate < todayEnd)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            var commit = await _db.SummaryCommits
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Date == today);

            return new SummaryResponse
            {
                Date = today,
                CreditCardEntries = creditCardEntries,
                LastSafe = safeDrop?.LastSafe ?? 0m,
                SafeDropAmount = safeDrop?.SafeDropAmount ?? 0m,
                Cash = safeDrop is null ? 0m : safeDrop.LastSafe + safeDrop.SafeDropAmount,
                Cashback = deduction?.Cashback ?? 0m,
                PaypointPayout = deduction?.PaypointPayout ?? 0m,
                InstantLotteryPayout = deduction?.InstantLotteryPayout ?? 0m,
                NewsVoucher = deduction?.NewsVoucher ?? 0m,
                DDPoint = deduction?.DDPoint ?? 0m,
                InstantLotteryTotalSales = instantLotteryTotalSales,
                LotteryValue = lottery?.LotteryValue ?? 0m,
                PaypointValue = paypoint?.PaypointValue ?? 0m,
                IsCommitted = commit is not null,
                CommittedAt = commit?.CommittedAt,
            };
        }

        public async Task<SummaryResponse> UpdateTodayAsync(int userId, SummaryUpdateRequest request)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var todayStart = DateTime.Today;
            var todayEnd = todayStart.AddDays(1);

            // Credit Card Banking — update existing rows or insert new ones
            foreach (var entry in request.CreditCardEntries)
            {
                if (entry.Id > 0)
                {
                    var existing = await _db.CreditCardBanking
                        .FirstOrDefaultAsync(c => c.Id == entry.Id && c.UserId == userId);

                    if (existing is not null)
                    {
                        existing.ManualCardAmount = entry.ManualCardAmount;
                        existing.CardAmount = entry.CardAmount;
                    }
                }
                else
                {
                    _db.CreditCardBanking.Add(new CreditCardBanking
                    {
                        UserId = userId,
                        ManualCardAmount = entry.ManualCardAmount,
                        CardAmount = entry.CardAmount,
                        CreatedDate = DateTime.UtcNow,
                    });
                }
            }

            // SafeDrop (Cash)
            var safeDrop = await _db.SafeDrops
                .FirstOrDefaultAsync(s => s.Date == today);

            if (safeDrop is null)
            {
                _db.SafeDrops.Add(new SafeDrop
                {
                    Date = today,
                    LastSafe = request.LastSafe,
                    SafeDropAmount = request.SafeDropAmount,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                safeDrop.LastSafe = request.LastSafe;
                safeDrop.SafeDropAmount = request.SafeDropAmount;
                safeDrop.UpdatedAt = DateTime.UtcNow;
            }

            // Deductions
            var deduction = await _db.Deductions
                .Where(d => d.UserId == userId
                         && d.CreatedAt >= todayStart
                         && d.CreatedAt < todayEnd)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (deduction is null)
            {
                _db.Deductions.Add(new Deduction
                {
                    UserId = userId,
                    Cashback = request.Cashback,
                    PaypointPayout = request.PaypointPayout,
                    InstantLotteryPayout = request.InstantLotteryPayout,
                    NewsVoucher = request.NewsVoucher,
                    DDPoint = request.DDPoint,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                deduction.Cashback = request.Cashback;
                deduction.PaypointPayout = request.PaypointPayout;
                deduction.InstantLotteryPayout = request.InstantLotteryPayout;
                deduction.NewsVoucher = request.NewsVoucher;
                deduction.DDPoint = request.DDPoint;
            }

            // Lottery Management
            var lottery = await _db.Lotteries
                .Where(l => l.UserId == userId
                         && l.CreatedDate >= todayStart
                         && l.CreatedDate < todayEnd)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            if (lottery is null)
            {
                _db.Lotteries.Add(new Lottery
                {
                    UserId = userId,
                    LotteryValue = request.LotteryValue,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                });
            }
            else
            {
                lottery.LotteryValue = request.LotteryValue;
                lottery.UpdatedDate = DateTime.UtcNow;
            }

            // Paypoint Management
            var paypoint = await _db.Paypoints
                .Where(p => p.UserId == userId
                         && p.CreatedDate >= todayStart
                         && p.CreatedDate < todayEnd)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            if (paypoint is null)
            {
                _db.Paypoints.Add(new Paypoint
                {
                    UserId = userId,
                    PaypointValue = request.PaypointValue,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                });
            }
            else
            {
                paypoint.PaypointValue = request.PaypointValue;
                paypoint.UpdatedDate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return await GetTodayAsync(userId);
        }

        public async Task<ZReportComparisonResponse> GetZReportComparisonAsync(int userId)
        {
            var summary = await GetTodayAsync(userId);

            var emails = await _gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z Report",
                MaxResults = 1
            });

            if (emails.Count == 0)
                throw new InvalidOperationException("No Z-report email found. Please ensure the Z-report has been received.");

            var body = emails[0].Body;

            // Parse Z-report fields
            var zManualCard     = ParseField(body, @"^MANUAL\s+CARD\s+([\d,]+\.?\d*)");
            var zCard           = ParseField(body, @"^CARD\s+([\d,]+\.?\d*)");
            var zCash           = ParseField(body, @"^CASH(?!\s+BACK)\s+([\d,]+\.?\d*)");
            var zCashBack       = ParseAbs  (body, @"CASH\s+BACK\s*\(Net\)\s+([-\d,]+\.?\d*)");
            var zInstPO         = ParseAbs  (body, @"^INST\s+PO\s+([-\d,]+\.?\d*)");
            var zPaypointPO     = ParseAbs  (body, @"^PAYPOINT\s+PO\s+([-\d,]+\.?\d*)");
            var zVoucher        = ParseAbs  (body, @"^VOUCHER\s+([-\d,]+\.?\d*)");
            var zDdRedeem       = ParseAbs  (body, @"^DD\s+REDEEM\s+([-\d,]+\.?\d*)");
            var zInstantLottery = ParseField(body, @"^INSTANT\s+LOTTERY\s+([\d,]+\.?\d*)");
            var zLottery        = ParseField(body, @"^LOTTERY\s+([\d,]+\.?\d*)");
            var zPaypoint       = ParseField(body, @"^PAYPOINT(?!\s+PO)\s+([\d,]+\.?\d*)");
            var zGrandTotal     = ParseField(body, @"^GRAND\s+TOTAL\s+([\d,]+\.?\d*)");

            var userManualCard = summary.CreditCardEntries.Sum(e => e.ManualCardAmount);
            var userCard       = summary.CreditCardEntries.Sum(e => e.CardAmount);
            var userCash       = summary.Cash;
            var userTotal      = userCash + userCard + userManualCard;
            var totalDiff      = Math.Abs(userTotal - zGrandTotal);

            var fields = new List<ZReportFieldComparison>
            {
                Cmp("Credit Card Banking",    "Manual Card Amount",       userManualCard,                  zManualCard),
                Cmp("Credit Card Banking",    "Card Amount",               userCard,                        zCard),
                Cmp("Cash Banking",           "Cash",                      userCash,                        zCash),
                Cmp("Deductions",             "Cashback",                  summary.Cashback,                zCashBack),
                Cmp("Deductions",             "Paypoint Payout",           summary.PaypointPayout,          zPaypointPO),
                Cmp("Deductions",             "Instant Lottery Payout",    summary.InstantLotteryPayout,    zInstPO),
                Cmp("Deductions",             "News Voucher",              summary.NewsVoucher,             zVoucher),
                Cmp("Deductions",             "DD Point",                  summary.DDPoint,                 zDdRedeem),
                Cmp("Instant Lottery",        "Total Sales",               summary.InstantLotteryTotalSales, zInstantLottery),
                Cmp("Lottery Management",     "Lottery Value",             summary.LotteryValue,            zLottery),
                Cmp("Paypoint Management",    "Paypoint Value",            summary.PaypointValue,           zPaypoint),
            };

            return new ZReportComparisonResponse
            {
                Date             = summary.Date,
                UserTotal        = userTotal,
                ZReportGrandTotal = zGrandTotal,
                TotalDifference  = totalDiff,
                CanCommit        = totalDiff <= 5m,
                Fields           = fields,
            };
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static ZReportFieldComparison Cmp(string section, string field, decimal user, decimal zReport) =>
            new() { Section = section, Field = field, UserValue = user, ZReportValue = zReport, Difference = user - zReport };

        private static decimal ParseField(string body, string pattern)
        {
            var m = Regex.Match(body, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!m.Success) return 0m;
            return decimal.TryParse(
                m.Groups[1].Value.Replace(",", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v) ? v : 0m;
        }

        private static decimal ParseAbs(string body, string pattern) =>
            Math.Abs(ParseField(body, pattern));

        public async Task<SummaryCommitResponse> CommitTodayAsync(int userId, SummaryCommitRequest request)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var existing = await _db.SummaryCommits
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Date == today);

            if (existing is not null)
                throw new InvalidOperationException("Today's summary is already committed.");

            // Run Z-report comparison to get full field details for the email
            ZReportComparisonResponse? comparison = null;
            try { comparison = await GetZReportComparisonAsync(userId); }
            catch { /* email sending is best-effort; fall back to request totals */ }

            var diff = comparison?.TotalDifference ?? Math.Abs(request.Difference);

            if (diff > 5.00m)
            {
                // Send variance alert email then block commit
                if (comparison is not null)
                    _ = _email.SendComparisonEmailAsync(comparison, committed: false);

                throw new InvalidOperationException(
                    $"Cannot commit — difference of £{diff:F2} exceeds the £5.00 limit. A notification email has been sent.");
            }

            var commit = new SummaryCommit
            {
                UserId       = userId,
                Date         = today,
                SummaryTotal = comparison?.UserTotal        ?? request.SummaryTotal,
                ZReportTotal = comparison?.ZReportGrandTotal ?? request.ZReportTotal,
                Difference   = diff,
                CommittedAt  = DateTime.UtcNow,
            };

            _db.SummaryCommits.Add(commit);
            await _db.SaveChangesAsync();

            // Send committed-successfully email
            if (comparison is not null)
                _ = _email.SendComparisonEmailAsync(comparison, committed: true);

            return new SummaryCommitResponse
            {
                Id           = commit.Id,
                Date         = commit.Date,
                SummaryTotal = commit.SummaryTotal,
                ZReportTotal = commit.ZReportTotal,
                Difference   = commit.Difference,
                CommittedAt  = commit.CommittedAt,
            };
        }
    }
}
