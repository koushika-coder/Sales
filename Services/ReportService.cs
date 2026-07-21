using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using System.Globalization;
using System.Text;

namespace Sales.Services
{
    public interface IReportService
    {
        Task<List<ReportListItem>> GetAllReportsAsync();
        Task<List<ReportListItem>> GetReportsAsync(DateOnly? startDate, DateOnly? endDate);
        Task<byte[]> GenerateReportsPdfAsync(DateOnly? startDate, DateOnly? endDate);
        Task<ReportDetailResponse?> GetReportByDateAsync(DateOnly date);
    }

    public class ReportService : IReportService
    {
        private readonly SalesDbContext _db;
        private readonly IGmailService _gmail;

        public ReportService(SalesDbContext db, IGmailService gmail)
        {
            _db = db;
            _gmail = gmail;
        }

        // ── All committed dates, newest first ────────────────────────────────

        public async Task<List<ReportListItem>> GetAllReportsAsync() => await GetReportsAsync(null, null);

        public async Task<List<ReportListItem>> GetReportsAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var commitsQuery = _db.SummaryCommits
                .Include(c => c.User)
                .AsQueryable();

            if (startDate.HasValue)
                commitsQuery = commitsQuery.Where(c => c.Date >= startDate.Value);
            if (endDate.HasValue)
                commitsQuery = commitsQuery.Where(c => c.Date <= endDate.Value);

            var commits = await commitsQuery
                .OrderByDescending(c => c.Date)
                .ToListAsync();

            var staffDates = commits.Select(c => c.Date).ToHashSet();

            var adminRecsQuery = _db.AdminReconciliations
                .Where(r => r.Status == "submitted");
            if (startDate.HasValue)
                adminRecsQuery = adminRecsQuery.Where(r => r.Date >= startDate.Value);
            if (endDate.HasValue)
                adminRecsQuery = adminRecsQuery.Where(r => r.Date <= endDate.Value);

            var adminRecs = await adminRecsQuery.ToListAsync();

            var adminRecByDate = adminRecs.ToDictionary(r => r.Date);

            var allUserIds = commits.Select(c => c.UserId)
                .Concat(adminRecs.Where(r => r.SubmittedByAdminId.HasValue)
                                 .Select(r => r.SubmittedByAdminId!.Value))
                .Distinct()
                .ToList();

            var userNames = await _db.Users
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            var result = new List<ReportListItem>();

            foreach (var commit in commits)
            {
                adminRecByDate.TryGetValue(commit.Date, out var adminRec);
                string? adminName = null;
                if (adminRec?.SubmittedByAdminId is int aid)
                    userNames.TryGetValue(aid, out adminName);

                var summaryTotal = adminRec?.SummaryTotal ?? commit.SummaryTotal;
                var zTotal       = adminRec?.ZReportTotal ?? commit.ZReportTotal;
                var variance     = adminRec?.Difference   ?? commit.Difference;

                result.Add(new ReportListItem
                {
                    Date            = commit.Date,
                    SummaryTotal    = summaryTotal,
                    ZReportTotal    = zTotal,
                    Variance        = variance,
                    WithinThreshold = variance <= 5m,
                    IsStaffCommitted  = true,
                    IsAdminReconciled = adminRec is not null,
                    CommittedAt       = commit.CommittedAt,
                    CommittedByUserId = commit.UserId,
                    CommittedByName   = commit.User?.Name ?? userNames.GetValueOrDefault(commit.UserId) ?? $"User #{commit.UserId}",
                    AdminSubmittedByAdminId = adminRec?.SubmittedByAdminId,
                    AdminSubmittedByName    = adminName,
                    AdminSubmittedAt        = adminRec?.SubmittedAt,
                });
            }

            foreach (var adminRec in adminRecs.Where(r => !staffDates.Contains(r.Date))
                                              .OrderByDescending(r => r.Date))
            {
                string? adminName = null;
                if (adminRec.SubmittedByAdminId is int aid)
                    userNames.TryGetValue(aid, out adminName);

                result.Add(new ReportListItem
                {
                    Date            = adminRec.Date,
                    SummaryTotal    = adminRec.SummaryTotal,
                    ZReportTotal    = adminRec.ZReportTotal,
                    Variance        = adminRec.Difference,
                    WithinThreshold = adminRec.Difference <= 5m,
                    IsStaffCommitted  = false,
                    IsAdminReconciled = true,
                    CommittedAt       = null,
                    CommittedByUserId = null,
                    CommittedByName   = null,
                    AdminSubmittedByAdminId = adminRec.SubmittedByAdminId,
                    AdminSubmittedByName    = adminName,
                    AdminSubmittedAt        = adminRec.SubmittedAt,
                });
            }

            return result.OrderByDescending(r => r.Date).ToList();
        }

        public async Task<byte[]> GenerateReportsPdfAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var reports = await GetReportsAsync(startDate, endDate);
            return await BuildDetailedPdfAsync(reports, startDate, endDate);
        }

        // ── Full detail for a single date ────────────────────────────────────

        public async Task<ReportDetailResponse?> GetReportByDateAsync(DateOnly date)
        {
            var commit = await _db.SummaryCommits
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Date == date);

            var adminRec = await _db.AdminReconciliations
                .FirstOrDefaultAsync(r => r.Date == date && r.Status == "submitted");

            if (commit is null && adminRec is null) return null;

            var rangeStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var rangeEnd   = rangeStart.AddDays(1);

            // Filter by the staff user who committed this date (if available).
            int? userId = commit?.UserId;

            // ── Staff entered values ──────────────────────────────────────────
            var ccBase = _db.CreditCardBanking
                .Where(c => c.CreatedDate >= rangeStart && c.CreatedDate < rangeEnd);
            if (userId.HasValue) ccBase = ccBase.Where(c => c.UserId == userId.Value);

            var manualCard = await ccBase.SumAsync(c => (decimal?)c.ManualCardAmount) ?? 0m;
            var cardAmount = await ccBase.SumAsync(c => (decimal?)c.CardAmount) ?? 0m;

            var safeDrop      = await _db.SafeDrops.FirstOrDefaultAsync(s => s.Date == date);
            var lastSafe      = safeDrop?.LastSafe ?? 0m;
            var safeDropAmount = safeDrop?.SafeDropAmount ?? 0m;
            var cash          = lastSafe + safeDropAmount;

            var dedBase = _db.Deductions
                .Where(d => d.CreatedAt >= rangeStart && d.CreatedAt < rangeEnd);
            if (userId.HasValue) dedBase = dedBase.Where(d => d.UserId == userId.Value);

            var deduction = await dedBase
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            var ilBase = _db.LotteryInventory
                .Where(li => li.InventoryDate >= rangeStart && li.InventoryDate < rangeEnd);
            if (userId.HasValue) ilBase = ilBase.Where(li => li.UserId == userId.Value);

            var ilSales = await ilBase.SumAsync(li => (decimal?)li.Sales) ?? 0m;

            var lotBase = _db.Lotteries
                .Where(l => l.CreatedDate >= rangeStart && l.CreatedDate < rangeEnd);
            if (userId.HasValue) lotBase = lotBase.Where(l => l.UserId == userId.Value);

            var lottery = await lotBase.OrderByDescending(l => l.CreatedDate).FirstOrDefaultAsync();

            var ppBase = _db.Paypoints
                .Where(p => p.CreatedDate >= rangeStart && p.CreatedDate < rangeEnd);
            if (userId.HasValue) ppBase = ppBase.Where(p => p.UserId == userId.Value);

            var paypoint = await ppBase.OrderByDescending(p => p.CreatedDate).FirstOrDefaultAsync();

            // ── Z-Report email matched by received date ───────────────────────
            var zValues    = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var zAvailable = false;

            try
            {
                var emails = await _gmail.GetEmailsAsync(new GmailRequest
                {
                    SubjectKeyword = "Z-Report",
                    MaxResults     = 50,
                });

                var zEmail = emails.FirstOrDefault(e =>
                    ParseEmailReceivedDate(e.Date) == date &&
                    !e.Body.TrimStart().StartsWith('<') &&
                    e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase));

                if (zEmail is not null)
                {
                    zValues    = ParseZReportLines(zEmail.Body);
                    zAvailable = zValues.Count > 0;
                }
            }
            catch { /* Gmail unavailable — fall back to stored totals */ }

            decimal Z(string key) => zValues.TryGetValue(key, out var v) ? v : 0m;
            decimal ZAbs(string key) => Math.Abs(Z(key));

            // ── Per-field comparison rows ─────────────────────────────────────
            var fields = new List<ReportFieldRow>
            {
                Row("Credit Card",    "Manual Card Amount",       manualCard,                              Z("MANUAL CARD")),
                Row("Credit Card",    "Card Amount",               cardAmount,                              Z("CARD")),
                Row("Cash",           "Last Safe",                 lastSafe,                                0m),
                Row("Cash",           "Safe Drop Amount",          safeDropAmount,                          0m),
                Row("Cash",           "Cash Total",                cash,                                    Z("CASH")),
                Row("Deductions",     "Cashback",                  deduction?.Cashback ?? 0m,               ZAbs("CASH BACK (Net)")),
                Row("Deductions",     "Paypoint Payout",           deduction?.PaypointPayout ?? 0m,         ZAbs("PAYPOINT PO")),
                Row("Deductions",     "Instant Lottery Payout",    deduction?.InstantLotteryPayout ?? 0m,   ZAbs("INST PO")),
                Row("Deductions",     "News Voucher",              deduction?.NewsVoucher ?? 0m,            ZAbs("VOUCHER")),
                Row("Deductions",     "DD Point",                  deduction?.DDPoint ?? 0m,                ZAbs("DD REDEEM")),
                Row("Deductions",     "Lottery Payout",            deduction?.LotteryPayout ?? 0m,          0m),
                Row("Instant Lottery","Total Sales",               ilSales,                                 Z("INSTANT LOTTERY")),
                Row("Lottery",        "Lottery Value",             lottery?.LotteryValue ?? 0m,             Z("LOTTERY")),
                Row("Paypoint",       "Paypoint Value",            paypoint?.PaypointValue ?? 0m,           Z("PAYPOINT")),
            };

            // ── Totals — use stored commit/adminRec values for accuracy ────────
            string? adminName = null;
            if (adminRec?.SubmittedByAdminId is int adminId)
            {
                adminName = await _db.Users
                    .Where(u => u.Id == adminId)
                    .Select(u => u.Name)
                    .FirstOrDefaultAsync();
            }

            var storedZTotal = adminRec?.ZReportTotal ?? commit?.ZReportTotal ?? 0m;
            var zTotal = zAvailable
                ? (Z("DEPARTMENT TOTAL") is var dt && dt > 0m ? dt : Z("GRAND TOTAL"))
                : storedZTotal;

            var staffTotal = adminRec?.SummaryTotal ?? commit?.SummaryTotal
                ?? (manualCard + cardAmount + cash
                    + (deduction?.Cashback ?? 0m) + (deduction?.PaypointPayout ?? 0m)
                    + (deduction?.InstantLotteryPayout ?? 0m) + (deduction?.NewsVoucher ?? 0m)
                    + (deduction?.DDPoint ?? 0m) + (deduction?.LotteryPayout ?? 0m)
                    + ilSales + (lottery?.LotteryValue ?? 0m) + (paypoint?.PaypointValue ?? 0m));

            var variance = Math.Abs(staffTotal - zTotal);

            return new ReportDetailResponse
            {
                Date              = date,
                Fields            = fields,
                StaffTotal        = staffTotal,
                ZReportTotal      = zTotal,
                TotalVariance     = variance,
                WithinThreshold   = variance <= 5m,
                CommittedByUserId = commit?.UserId,
                CommittedByName   = commit?.User?.Name ?? (commit != null ? $"User #{commit.UserId}" : null),
                CommittedAt       = commit?.CommittedAt,
                AdminSubmittedByAdminId = adminRec?.SubmittedByAdminId,
                AdminSubmittedByName    = adminName,
                AdminSubmittedAt        = adminRec?.SubmittedAt,
                ZReportAvailable        = zAvailable,
            };
        }

        private static DateOnly? ParseEmailReceivedDate(string emailDate)
        {
            if (string.IsNullOrWhiteSpace(emailDate)) return null;
            if (DateTimeOffset.TryParse(emailDate,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dto))
                return DateOnly.FromDateTime(dto.DateTime);
            return null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static ReportFieldRow Row(string section, string field, decimal staff, decimal z) =>
            new() { Section = section, Field = field, StaffValue = staff, ZReportValue = z, Variance = staff - z };

        private async Task<byte[]> BuildDetailedPdfAsync(IEnumerable<ReportListItem> reports, DateOnly? startDate, DateOnly? endDate)
        {
            var lines = new List<string>
            {
                "Reconciliation Reports",
                startDate.HasValue || endDate.HasValue
                    ? $"Period: {(startDate?.ToString("yyyy-MM-dd") ?? "start")} to {(endDate?.ToString("yyyy-MM-dd") ?? "end")}"
                    : "All available records",
                string.Empty
            };

            if (!reports.Any())
            {
                lines.Add("No reconciliation records found for the selected range.");
            }
            else
            {
                foreach (var report in reports)
                {
                    var status = report.IsAdminReconciled ? "Reconciled" : "Pending";
                    lines.Add($"Date: {report.Date:yyyy-MM-dd}");
                    lines.Add($"Summary: {report.SummaryTotal.ToString("F2", CultureInfo.InvariantCulture)}");
                    lines.Add($"Z-Report: {report.ZReportTotal.ToString("F2", CultureInfo.InvariantCulture)}");
                    lines.Add($"Variance: {report.Variance.ToString("F2", CultureInfo.InvariantCulture)}");
                    lines.Add($"Status: {status}");
                    lines.Add(string.Empty);

                    var detail = await GetReportByDateAsync(report.Date);
                    if (detail is null || detail.Fields.Count == 0)
                    {
                        lines.Add("No detailed breakdown available.");
                    }
                    else
                    {
                        lines.Add("Breakdown:");
                        foreach (var field in detail.Fields)
                        {
                            lines.Add($"- {field.Section} | {field.Field} | Staff: {field.StaffValue.ToString("F2", CultureInfo.InvariantCulture)} | Z: {field.ZReportValue.ToString("F2", CultureInfo.InvariantCulture)} | Variance: {field.Variance.ToString("F2", CultureInfo.InvariantCulture)}");
                        }
                    }

                    lines.Add(string.Empty);
                    lines.Add("------------------------------------------------------------");
                    lines.Add(string.Empty);
                }
            }

            var contentBuilder = new StringBuilder();
            var y = 760;
            foreach (var line in lines)
            {
                var escapedLine = EscapePdfText(line);
                contentBuilder.AppendLine($"BT /F1 10 Tf 72 {y} Td ({escapedLine}) Tj ET");
                y -= 12;
            }

            var content = contentBuilder.ToString();
            var contentBytes = Encoding.ASCII.GetBytes(content);
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

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

        private static Dictionary<string, decimal> ParseZReportLines(string body)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r').Trim();
                while (line.StartsWith('>'))
                    line = line.TrimStart('>').TrimStart();
                var lastSpace = line.LastIndexOf(' ');
                if (lastSpace < 0) continue;

                var label    = line[..lastSpace].TrimEnd();
                var valueStr = line[(lastSpace + 1)..].Trim();

                if (string.IsNullOrEmpty(label)) continue;

                if (decimal.TryParse(
                        valueStr.Replace(",", ""),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var val))
                {
                    result[label] = val;
                }
            }
            return result;
        }
    }
}
