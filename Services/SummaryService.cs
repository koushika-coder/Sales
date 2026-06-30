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
        Task<ZReportEmailResult> GetZReportEmailAsync(int userId);
        Task<ZReportEmailResult> GetZReportEmailByDateAsync(int userId, DateOnly date);
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

        // Returns yesterday if yesterday is uncommitted (by staff or admin), otherwise today.
        // If an admin has set an active-date override for this user, that takes priority
        // (and is automatically cleared once that date becomes committed).
        private async Task<DateOnly> GetActiveDateAsync(int userId)
        {
            var ovr = await _db.UserActiveDateOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId);

            if (ovr is not null)
            {
                if (!await IsDateCommittedAsync(userId, ovr.ActiveDate))
                    return ovr.ActiveDate;

                // Override date is already committed — clean it up and fall through.
                _db.UserActiveDateOverrides.Remove(ovr);
                await _db.SaveChangesAsync();
            }

            var today     = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            var todayCommitted =
                await _db.SummaryCommits.AnyAsync(c => c.UserId == userId && c.Date == today) ||
                await _db.AdminReconciliations.AnyAsync(r => r.Date == today && r.Status == "submitted");
            if (todayCommitted) return today.AddDays(1);

            var yesterdayCommitted =
                await _db.SummaryCommits.AnyAsync(c => c.UserId == userId && c.Date == yesterday) ||
                await _db.AdminReconciliations.AnyAsync(r => r.Date == yesterday && r.Status == "submitted");
            return yesterdayCommitted ? today : yesterday;
        }

        private async Task<bool> IsDateCommittedAsync(int userId, DateOnly date) =>
            await _db.SummaryCommits.AnyAsync(c => c.UserId == userId && c.Date == date) ||
            await _db.AdminReconciliations.AnyAsync(r => r.Date == date && r.Status == "submitted");

        public async Task<SummaryResponse> GetTodayAsync(int userId)
            => await GetSummaryForDateAsync(userId, await GetActiveDateAsync(userId));

        private async Task<SummaryResponse> GetSummaryForDateAsync(int userId, DateOnly date)
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end   = start.AddDays(1);

            var creditCardEntries = await _db.CreditCardBanking
                .Where(c => c.UserId == userId
                         && c.CreatedDate >= start
                         && c.CreatedDate < end)
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
                .FirstOrDefaultAsync(s => s.Date == date);

            var deduction = await _db.Deductions
                .Where(d => d.UserId == userId
                         && d.CreatedAt >= start
                         && d.CreatedAt < end)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            // Instant lottery: sum inventory for the active date first;
            // if nothing entered yet, fall back to the most recent uncommitted date's inventory.
            var instantLotteryQuery = _db.LotteryInventory
                .Where(li => li.UserId == userId
                          && li.InventoryDate >= start
                          && li.InventoryDate < end);

            var instantLotteryTotalCount = await instantLotteryQuery.SumAsync(li => (int?)li.TotalSold) ?? 0;
            var instantLotteryTotalSales = await instantLotteryQuery.SumAsync(li => (decimal?)li.Sales) ?? 0m;

            if (instantLotteryTotalCount == 0 && instantLotteryTotalSales == 0m)
            {
                var latestInventoryTs = await _db.LotteryInventory
                    .Where(li => li.UserId == userId && li.InventoryDate < start)
                    .MaxAsync(li => (DateTime?)li.InventoryDate);

                if (latestInventoryTs.HasValue)
                {
                    var latestDate = DateOnly.FromDateTime(latestInventoryTs.Value);
                    var latestCommitted = await IsDateCommittedAsync(userId, latestDate);

                    if (!latestCommitted)
                    {
                        var fallback = _db.LotteryInventory
                            .Where(li => li.UserId == userId && li.InventoryDate == latestInventoryTs.Value);
                        instantLotteryTotalCount = await fallback.SumAsync(li => (int?)li.TotalSold) ?? 0;
                        instantLotteryTotalSales = await fallback.SumAsync(li => (decimal?)li.Sales) ?? 0m;
                    }
                }
            }

            // Lottery value: active date first, then most recent uncommitted record.
            var lottery = await _db.Lotteries
                .Where(l => l.UserId == userId
                         && l.CreatedDate >= start
                         && l.CreatedDate < end)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            if (lottery == null)
            {
                var prevLottery = await _db.Lotteries
                    .Where(l => l.UserId == userId && l.CreatedDate < start)
                    .OrderByDescending(l => l.CreatedDate)
                    .FirstOrDefaultAsync();
                if (prevLottery != null && !await IsDateCommittedAsync(userId, DateOnly.FromDateTime(prevLottery.CreatedDate)))
                    lottery = prevLottery;
            }

            // Paypoint value: active date first, then most recent uncommitted record.
            var paypoint = await _db.Paypoints
                .Where(p => p.UserId == userId
                         && p.CreatedDate >= start
                         && p.CreatedDate < end)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            if (paypoint == null)
            {
                var prevPaypoint = await _db.Paypoints
                    .Where(p => p.UserId == userId && p.CreatedDate < start)
                    .OrderByDescending(p => p.CreatedDate)
                    .FirstOrDefaultAsync();
                if (prevPaypoint != null && !await IsDateCommittedAsync(userId, DateOnly.FromDateTime(prevPaypoint.CreatedDate)))
                    paypoint = prevPaypoint;
            }

            var commit = await _db.SummaryCommits
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Date == date);

            var hasInventoryToday = await _db.LotteryInventory
                .AnyAsync(li => li.UserId == userId && li.InventoryDate >= start && li.InventoryDate < end);

            var hasTodayData = deduction != null
                || safeDrop != null
                || creditCardEntries.Any()
                || hasInventoryToday
                || lottery != null
                || paypoint != null;

            var lastSafe    = safeDrop?.LastSafe      ?? 0m;
            var closeAmount = safeDrop?.SafeDropAmount ?? 0m;

            return new SummaryResponse
            {
                Date = date,
                CreditCardEntries = creditCardEntries,
                LastSafe = lastSafe,
                SafeDropAmount = closeAmount,
                Cash = lastSafe + closeAmount,
                Cashback = deduction?.Cashback ?? 0m,
                PaypointPayout = deduction?.PaypointPayout ?? 0m,
                InstantLotteryPayout = deduction?.InstantLotteryPayout ?? 0m,
                NewsVoucher = deduction?.NewsVoucher ?? 0m,
                DDPoint = deduction?.DDPoint ?? 0m,
                LotteryPayout = deduction?.LotteryPayout ?? 0m,
                InstantLotteryTotalCount = instantLotteryTotalCount,
                InstantLotteryTotalSales = instantLotteryTotalSales,
                LotteryValue = lottery?.LotteryValue ?? 0m,
                PaypointValue = paypoint?.PaypointValue ?? 0m,
                IsCommitted = commit is not null,
                CommittedAt = commit?.CommittedAt,
                HasTodayData = hasTodayData,
            };
        }

        public async Task<SummaryResponse> UpdateTodayAsync(int userId, SummaryUpdateRequest request)
        {
            var activeDate  = await GetActiveDateAsync(userId);
            var activeStart = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var activeEnd   = activeStart.AddDays(1);
            var recordAt    = activeDate == DateOnly.FromDateTime(DateTime.UtcNow)
                                ? DateTime.UtcNow
                                : activeStart.AddHours(12);

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
                        CreatedDate = recordAt,
                    });
                }
            }

            // SafeDrop (Cash) — both LastSafe and SafeDropAmount are user-entered
            var safeDrop = await _db.SafeDrops
                .FirstOrDefaultAsync(s => s.Date == activeDate);

            if (safeDrop is null)
            {
                _db.SafeDrops.Add(new SafeDrop
                {
                    Date = activeDate,
                    LastSafe = request.LastSafe,
                    SafeDropAmount = request.SafeDropAmount,
                    CreatedAt = recordAt,
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
                         && d.CreatedAt >= activeStart
                         && d.CreatedAt < activeEnd)
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
                    LotteryPayout = request.LotteryPayout,
                    CreatedAt = recordAt,
                });
            }
            else
            {
                deduction.Cashback = request.Cashback;
                deduction.PaypointPayout = request.PaypointPayout;
                deduction.InstantLotteryPayout = request.InstantLotteryPayout;
                deduction.NewsVoucher = request.NewsVoucher;
                deduction.DDPoint = request.DDPoint;
                deduction.LotteryPayout = request.LotteryPayout;
            }

            // Lottery Management
            var lottery = await _db.Lotteries
                .Where(l => l.UserId == userId
                         && l.CreatedDate >= activeStart
                         && l.CreatedDate < activeEnd)
                .OrderByDescending(l => l.CreatedDate)
                .FirstOrDefaultAsync();

            if (lottery is null)
            {
                _db.Lotteries.Add(new Lottery
                {
                    UserId = userId,
                    LotteryValue = request.LotteryValue,
                    CreatedDate = recordAt,
                    UpdatedDate = recordAt,
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
                         && p.CreatedDate >= activeStart
                         && p.CreatedDate < activeEnd)
                .OrderByDescending(p => p.CreatedDate)
                .FirstOrDefaultAsync();

            if (paypoint is null)
            {
                _db.Paypoints.Add(new Paypoint
                {
                    UserId = userId,
                    PaypointValue = request.PaypointValue,
                    CreatedDate = recordAt,
                    UpdatedDate = recordAt,
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
            var targetDate = await GetActiveDateAsync(userId);

            if (await IsDateCommittedAsync(userId, targetDate))
                throw new InvalidOperationException(
                    $"Values for {targetDate:dd-MM-yyyy} are already committed.");

            var summary = await GetSummaryForDateAsync(userId, targetDate);

            // Search recent Z-report emails without a date-range filter — the email may
            // arrive days after the POS date, so we match by the date inside the body.
            var emails = await _gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z-Report",
                MaxResults     = 50,
            });

            // Require plain text, "GRAND TOTAL" marker, and body POS date == active date.
            var zEmail = emails.FirstOrDefault(e =>
                    !e.Body.TrimStart().StartsWith('<') &&
                    e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) &&
                    ParseEmailReceivedDate(e.Date) == targetDate)
                ?? throw new InvalidOperationException($"No Z-report email found for {targetDate:dd-MM-yyyy}. Please ensure the plain-text Z-report has been received.");

            var body = zEmail.Body;

            // Line-by-line parser: each line is "LABEL   VALUE" — the last whitespace-separated
            // token is the numeric value, the rest is the label. This avoids multiline ^ issues.
            var z = ParseZReportLines(body);

            decimal GetZ(string label) => z.TryGetValue(label, out var v) ? v : 0m;

            // Prefer "DEPARTMENT TOTAL" if present, otherwise fall back to "GRAND TOTAL"
            var zDeptTotal = z.TryGetValue("DEPARTMENT TOTAL", out var dt) ? dt
                           : z.TryGetValue("DEPT TOTAL",       out var dt2) ? dt2
                           : GetZ("GRAND TOTAL");

            // Supplier invoices total for the date (all users — store-level like the Z-report)
            var invStart = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var invEnd   = invStart.AddDays(1);
            var supplierInvoicesTotal = await _db.SupplierInvoices
                .Where(i => i.CreatedAt >= invStart && i.CreatedAt < invEnd)
                .SumAsync(i => (decimal?)i.Value) ?? 0m;

            var userManualCard = summary.CreditCardEntries.Sum(e => e.ManualCardAmount);
            var userCard       = summary.CreditCardEntries.Sum(e => e.CardAmount);

            // Sum of all user-entered values compared against the Z-report department total
            var userTotal = userManualCard
                          + userCard
                          + summary.Cash
                          + summary.Cashback
                          + summary.PaypointPayout
                          + summary.InstantLotteryPayout
                          + summary.NewsVoucher
                          + summary.DDPoint
                          + summary.LotteryPayout
                          + summary.InstantLotteryTotalSales
                          + summary.LotteryValue
                          + summary.PaypointValue
                          + supplierInvoicesTotal;

            var totalDiff = Math.Abs(userTotal - zDeptTotal);

            var fields = new List<ZReportFieldComparison>
            {
                Cmp("Credit Card Banking", "Manual Card Amount",    userManualCard,                   0),
                Cmp("Credit Card Banking", "Card Amount",            userCard,                         0),
                Cmp("Cash Banking",        "Cash",                   summary.Cash,                     0),
                Cmp("Deductions",          "Cashback",               summary.Cashback,                 0),
                Cmp("Deductions",          "Paypoint Payout",        summary.PaypointPayout,           0),
                Cmp("Deductions",          "Instant Lottery Payout", summary.InstantLotteryPayout,     0),
                Cmp("Deductions",          "News Voucher",           summary.NewsVoucher,              0),
                Cmp("Deductions",          "DD Point",               summary.DDPoint,                  0),
                Cmp("Deductions",          "Lottery Payout",         summary.LotteryPayout,            0),
                Cmp("Instant Lottery",     "Total Sales",            summary.InstantLotteryTotalSales, 0),
                Cmp("Lottery Management",  "Lottery Value",          summary.LotteryValue,             0),
                Cmp("Paypoint Management", "Paypoint Value",         summary.PaypointValue,            0),
                Cmp("Supplier Invoices",   "Total Invoices",         supplierInvoicesTotal,            0),
            };

            return new ZReportComparisonResponse
            {
                Date              = summary.Date,
                UserTotal         = userTotal,
                ZReportGrandTotal = zDeptTotal,
                TotalDifference   = totalDiff,
                CanCommit         = totalDiff <= 5m,
                Fields            = fields,
            };
        }

        public async Task<ZReportEmailResult> GetZReportEmailAsync(int userId)
        {
            var targetDate = await GetActiveDateAsync(userId);

            if (await IsDateCommittedAsync(userId, targetDate))
            {
                return new ZReportEmailResult
                {
                    IsCommitted = true,
                    TargetDate  = targetDate,
                    Message     = $"Values for {targetDate:dd-MM-yyyy} are already committed.",
                    Email       = null,
                };
            }

            var emails = await _gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z-Report",
                MaxResults     = 50,
            });

            var zEmail = emails.FirstOrDefault(e =>
                    !e.Body.TrimStart().StartsWith('<') &&
                    e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) &&
                    ParseEmailReceivedDate(e.Date) == targetDate)
                ?? throw new InvalidOperationException(
                    $"No Z-report email found for {targetDate:dd-MM-yyyy}. Please ensure the plain-text Z-report has been received.");

            return new ZReportEmailResult
            {
                IsCommitted = false,
                TargetDate  = targetDate,
                Message     = null,
                Email       = zEmail,
            };
        }

        public async Task<ZReportEmailResult> GetZReportEmailByDateAsync(int userId, DateOnly date)
        {
            if (await IsDateCommittedAsync(userId, date))
            {
                return new ZReportEmailResult
                {
                    IsCommitted = true,
                    TargetDate  = date,
                    Message     = $"Values for {date:dd-MM-yyyy} are already committed.",
                    Email       = null,
                };
            }

            var emails = await _gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z-Report",
                MaxResults     = 50,
            });

            var zEmail = emails.FirstOrDefault(e =>
                    !e.Body.TrimStart().StartsWith('<') &&
                    e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase) &&
                    ParseEmailReceivedDate(e.Date) == date)
                ?? throw new InvalidOperationException(
                    $"No Z-report email found for {date:dd-MM-yyyy}. Please ensure the plain-text Z-report has been received.");

            return new ZReportEmailResult
            {
                IsCommitted = false,
                TargetDate  = date,
                Message     = null,
                Email       = zEmail,
            };
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static ZReportFieldComparison Cmp(string section, string field, decimal user, decimal zReport) =>
            new() { Section = section, Field = field, UserValue = user, ZReportValue = zReport, Difference = user - zReport };

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

        // Parses the Z-report plain-text body line by line.
        // Each line has the form "LABEL   <number>" — we take the last whitespace-separated
        // token as the value and everything before it (trimmed) as the label.
        private static Dictionary<string, decimal> ParseZReportLines(string body)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r').Trim();
                // Strip email reply quote characters ("> ", ">> ", etc.)
                while (line.StartsWith('>'))
                    line = line.TrimStart('>').TrimStart();
                var lastSpace = line.LastIndexOf(' ');
                if (lastSpace < 0) continue;

                var label    = line[..lastSpace].TrimEnd();
                var valueStr = line[(lastSpace + 1)..].Trim();

                if (string.IsNullOrEmpty(label)) continue;

                var cleaned = valueStr.Replace(",", "").Replace("£", "").Replace("$", "").Trim();
                if (decimal.TryParse(
                        cleaned,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var val))
                {
                    result[label] = val;
                }
            }
            return result;
        }

        public async Task<SummaryCommitResponse> CommitTodayAsync(int userId, SummaryCommitRequest request)
        {
            var activeDate = await GetActiveDateAsync(userId);

            var existing = await _db.SummaryCommits
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Date == activeDate);

            if (existing is not null)
                throw new InvalidOperationException("This day's summary is already committed.");

            // Try to get full Z-report field breakdown for the email
            ZReportComparisonResponse? comparison = null;
            try { comparison = await GetZReportComparisonAsync(userId); }
            catch { }

            var diff = comparison?.TotalDifference ?? Math.Abs(request.Difference);

            ZReportComparisonResponse emailPayload;
            if (comparison is not null)
            {
                emailPayload = comparison;
            }
            else
            {
                // Gmail unavailable — calculate UserTotal from the DB so it is never stored as 0
                var summary        = await GetSummaryForDateAsync(userId, activeDate);
                var fbStart        = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var fbEnd          = fbStart.AddDays(1);
                var fbInvoices     = await _db.SupplierInvoices
                    .Where(i => i.CreatedAt >= fbStart && i.CreatedAt < fbEnd)
                    .SumAsync(i => (decimal?)i.Value) ?? 0m;
                var userManualCard  = summary.CreditCardEntries.Sum(e => e.ManualCardAmount);
                var userCard        = summary.CreditCardEntries.Sum(e => e.CardAmount);
                var calculatedTotal = userManualCard
                                    + userCard
                                    + summary.Cash
                                    + summary.Cashback
                                    + summary.PaypointPayout
                                    + summary.InstantLotteryPayout
                                    + summary.NewsVoucher
                                    + summary.DDPoint
                                    + summary.LotteryPayout
                                    + summary.InstantLotteryTotalSales
                                    + summary.LotteryValue
                                    + summary.PaypointValue
                                    + fbInvoices;

                emailPayload = new ZReportComparisonResponse
                {
                    Date              = activeDate,
                    UserTotal         = calculatedTotal,
                    ZReportGrandTotal = request.ZReportTotal,
                    TotalDifference   = diff,
                    CanCommit         = diff <= 5m,
                    Fields            = [],
                };
            }

            if (diff > 5.00m)
            {
                await SaveFailedCommitAsPendingAsync(userId, activeDate, emailPayload, diff);
                await _email.SendComparisonEmailAsync(emailPayload, committed: false);
                throw new InvalidOperationException(
                    $"Cannot commit — difference of £{diff:F2} exceeds the £5.00 limit. A notification email has been sent.");
            }

            var commit = new SummaryCommit
            {
                UserId       = userId,
                Date         = activeDate,
                SummaryTotal = emailPayload.UserTotal,
                ZReportTotal = emailPayload.ZReportGrandTotal,
                Difference   = diff,
                CommittedAt  = DateTime.UtcNow,
            };

            _db.SummaryCommits.Add(commit);
            await _db.SaveChangesAsync();

            try { await _email.SendComparisonEmailAsync(emailPayload, committed: true); }
            catch { /* email failure must not roll back a successful commit */ }

            // After the commit the active date shifts forward — fetch the new date's summary
            // so the frontend can immediately display the next day's (empty) dashboard.
            var newSummary = await GetTodayAsync(userId);

            return new SummaryCommitResponse
            {
                Id           = commit.Id,
                Date         = commit.Date,
                SummaryTotal = commit.SummaryTotal,
                ZReportTotal = commit.ZReportTotal,
                Difference   = commit.Difference,
                CommittedAt  = commit.CommittedAt,
                NewSummary   = newSummary,
            };
        }

        // A commit attempt that exceeds the £5.00 limit is recorded immediately as a
        // "pending" AdminReconciliation row so it shows up for admin review right away,
        // instead of waiting for the date-scan loop to pick it up the next day.
        private async Task SaveFailedCommitAsPendingAsync(
            int userId, DateOnly activeDate, ZReportComparisonResponse emailPayload, decimal diff)
        {
            var summary = await GetSummaryForDateAsync(userId, activeDate);

            var existing = await _db.AdminReconciliations
                .FirstOrDefaultAsync(r => r.Date == activeDate && r.Status == "pending");

            var row = existing ?? new AdminReconciliation { Date = activeDate, CreatedAt = DateTime.UtcNow };
            if (existing is null) _db.AdminReconciliations.Add(row);

            row.ManualCardAmount         = summary.CreditCardEntries.Sum(e => e.ManualCardAmount);
            row.CardAmount               = summary.CreditCardEntries.Sum(e => e.CardAmount);
            row.LastSafe                 = summary.LastSafe;
            row.SafeDropAmount           = summary.SafeDropAmount;
            row.Cashback                 = summary.Cashback;
            row.PaypointPayout           = summary.PaypointPayout;
            row.InstantLotteryPayout     = summary.InstantLotteryPayout;
            row.NewsVoucher              = summary.NewsVoucher;
            row.DDPoint                  = summary.DDPoint;
            row.LotteryPayout            = summary.LotteryPayout;
            row.InstantLotteryTotalCount = summary.InstantLotteryTotalCount;
            row.InstantLotteryTotalSales = summary.InstantLotteryTotalSales;
            row.LotteryValue             = summary.LotteryValue;
            row.PaypointValue            = summary.PaypointValue;
            row.SummaryTotal             = emailPayload.UserTotal;
            row.ZReportTotal             = emailPayload.ZReportGrandTotal;
            row.Difference               = diff;
            row.Status                   = "pending";
            row.UpdatedAt                = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
    }
}
