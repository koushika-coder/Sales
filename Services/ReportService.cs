using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Sales.Services
{
    public interface IReportService
    {
        Task<List<ReportListItem>> GetAllReportsAsync();
        Task<List<ReportListItem>> GetReportsAsync(DateOnly? startDate, DateOnly? endDate);

        // Returns a single-date PDF, or (when the range spans more than one date) a ZIP
        // containing one PDF per date — so a multi-date download is a bundle of clean
        // single-page reports instead of one long unpaginated document.
        Task<(string FileName, byte[] Bytes, string ContentType)> GenerateReportsDownloadAsync(DateOnly? startDate, DateOnly? endDate);
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

        public async Task<(string FileName, byte[] Bytes, string ContentType)> GenerateReportsDownloadAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var reports = await GetReportsAsync(startDate, endDate);
            var from = startDate?.ToString("yyyy-MM-dd") ?? "start";
            var to   = endDate?.ToString("yyyy-MM-dd") ?? "end";
            var hasRange = startDate.HasValue || endDate.HasValue;

            if (reports.Count == 0)
            {
                var rangeLabel = hasRange ? $"{from} to {to}" : "All available records";
                var bytes = BuildEmptyReportPdf(rangeLabel);
                var fileName = hasRange ? $"reconciliation-reports-{from}-to-{to}.pdf" : "reconciliation-reports.pdf";
                return (fileName, bytes, "application/pdf");
            }

            if (reports.Count == 1)
            {
                var detail = await GetReportByDateAsync(reports[0].Date);
                var bytes = BuildSingleReportPdf(reports[0], detail);
                return ($"reconciliation-report-{reports[0].Date:yyyy-MM-dd}.pdf", bytes, "application/pdf");
            }

            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var report in reports)
                {
                    var detail = await GetReportByDateAsync(report.Date);
                    var bytes = BuildSingleReportPdf(report, detail);
                    var entry = archive.CreateEntry($"{report.Date:yyyy-MM-dd}.pdf", CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(bytes);
                }
            }

            return ($"reconciliation-reports-{from}-to-{to}.zip", zipStream.ToArray(), "application/zip");
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

        // ── PDF rendering ─────────────────────────────────────────────────────
        // Hand-rolled raw PDF (no external library, matching the rest of this codebase's
        // approach) but with real drawing primitives — filled/stroked rectangles and
        // lines plus positioned, colored text — so the report reads as an actual
        // designed document (banded header, stat cards, a bordered/striped table with
        // colour-coded variance) instead of a plain monospaced text dump. Each report
        // always fits on a single page, since the field list is fixed-length, which also
        // sidesteps the previous version's bug of silently truncating content that ran
        // past one page with no pagination.

        private const double PdfPageWidth = 612;   // US Letter, points
        private const double PdfPageHeight = 792;
        private const double PdfMargin = 40;

        private static readonly (double R, double G, double B) ColorBrand   = (0.31, 0.27, 0.90); // indigo
        private static readonly (double R, double G, double B) ColorWhite   = (1, 1, 1);
        private static readonly (double R, double G, double B) ColorText    = (0.12, 0.16, 0.22);
        private static readonly (double R, double G, double B) ColorMuted   = (0.42, 0.45, 0.50);
        private static readonly (double R, double G, double B) ColorFaint   = (0.66, 0.69, 0.74);
        private static readonly (double R, double G, double B) ColorBorder  = (0.85, 0.87, 0.91);
        private static readonly (double R, double G, double B) ColorStripe  = (0.97, 0.98, 0.99);
        private static readonly (double R, double G, double B) ColorGreen   = (0.09, 0.64, 0.29);
        private static readonly (double R, double G, double B) ColorGreenBg = (0.94, 0.99, 0.96);
        private static readonly (double R, double G, double B) ColorAmber   = (0.85, 0.47, 0.02);
        private static readonly (double R, double G, double B) ColorRed     = (0.86, 0.15, 0.15);
        private static readonly (double R, double G, double B) ColorRedBg   = (0.996, 0.95, 0.95);
        private static readonly (double R, double G, double B) ColorCardBg  = (0.96, 0.96, 0.99);

        private static byte[] BuildEmptyReportPdf(string rangeLabel)
        {
            var c = new StringBuilder();
            PdfFillRect(c, 0, PdfPageHeight - 58, PdfPageWidth, 58, ColorBrand);
            PdfText(c, PdfMargin, PdfPageHeight - 36, "Reconciliation Reports", "F2", 20, ColorWhite);
            PdfText(c, PdfMargin, PdfPageHeight - 92, $"Period: {rangeLabel}", "F1", 11, ColorMuted);
            PdfText(c, PdfMargin, PdfPageHeight - 114, "No reconciliation records found for the selected range.", "F1", 10, ColorMuted);
            return AssembleSinglePagePdf(c.ToString());
        }

        private static byte[] BuildSingleReportPdf(ReportListItem summary, ReportDetailResponse? detail)
        {
            var c = new StringBuilder();
            var contentW = PdfPageWidth - PdfMargin * 2;

            var staffTotal      = detail?.StaffTotal ?? summary.SummaryTotal;
            var zTotal          = detail?.ZReportTotal ?? summary.ZReportTotal;
            var variance        = detail?.TotalVariance ?? summary.Variance;
            var withinThreshold = detail?.WithinThreshold ?? summary.WithinThreshold;
            var isReconciled    = summary.IsAdminReconciled;

            // ── Header band ──
            const double bandH = 58;
            var bandY = PdfPageHeight - bandH;
            PdfFillRect(c, 0, bandY, PdfPageWidth, bandH, ColorBrand);
            PdfText(c, PdfMargin, bandY + bandH - 24, "Reconciliation Report", "F2", 20, ColorWhite);
            var dateLabel = summary.Date.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture);
            PdfText(c, PdfMargin, bandY + 14, dateLabel, "F1", 11, (0.90, 0.90, 0.99));

            var status = isReconciled ? "RECONCILED" : "PENDING";
            var statusColor = isReconciled ? ColorGreen : ColorAmber;
            var pillW = 24 + status.Length * EstimateTextWidth("A", "F2", 9);
            const double pillH = 20;
            var pillX = PdfPageWidth - PdfMargin - pillW;
            var pillY = bandY + bandH - 36;
            PdfFillRect(c, pillX, pillY, pillW, pillH, ColorWhite);
            PdfText(c, pillX + 12, pillY + 6, status, "F2", 9, statusColor);

            var y = bandY - 26;

            // ── Stat cards ──
            const double cardGap = 16;
            var cardW = (contentW - cardGap * 2) / 3;
            const double cardH = 54;
            var cardY = y - cardH;

            DrawStatCard(c, PdfMargin, cardY, cardW, cardH, "STAFF TOTAL", $"£{staffTotal:F2}", ColorCardBg, ColorBrand);
            DrawStatCard(c, PdfMargin + cardW + cardGap, cardY, cardW, cardH, "Z-REPORT TOTAL", $"£{zTotal:F2}", ColorCardBg, ColorBrand);

            var varColor = withinThreshold ? ColorGreen : ColorRed;
            var varBg    = withinThreshold ? ColorGreenBg : ColorRedBg;
            DrawStatCard(c, PdfMargin + (cardW + cardGap) * 2, cardY, cardW, cardH, "VARIANCE", $"£{variance:F2}", varBg, varColor);

            y = cardY - 28;

            // ── Breakdown table ──
            PdfText(c, PdfMargin, y, "Breakdown", "F2", 12, ColorText);
            y -= 18;

            var fields = detail?.Fields ?? new List<ReportFieldRow>();
            double[] colW = { 95, 150, 90, 90, contentW - (95 + 150 + 90 + 90) };
            string[] headers = { "Section", "Field", "Staff", "Z-Report", "Variance" };
            const double rowH = 20;
            const double headerH = 22;

            var tableTop = y;
            PdfFillRect(c, PdfMargin, tableTop - headerH, contentW, headerH, ColorBrand);

            var tx = PdfMargin;
            for (var i = 0; i < headers.Length; i++)
            {
                DrawCellText(c, tx, tableTop - headerH, colW[i], headerH, headers[i], "F2", 9, ColorWhite, i >= 2);
                tx += colW[i];
            }

            var rowY = tableTop - headerH;
            if (fields.Count == 0)
            {
                rowY -= rowH;
                PdfFillRect(c, PdfMargin, rowY, contentW, rowH, ColorWhite);
                DrawCellText(c, PdfMargin, rowY, contentW, rowH, "No detailed breakdown available.", "F1", 9, ColorMuted, false);
            }
            else
            {
                for (var r = 0; r < fields.Count; r++)
                {
                    rowY -= rowH;
                    var f = fields[r];
                    PdfFillRect(c, PdfMargin, rowY, contentW, rowH, r % 2 == 0 ? ColorWhite : ColorStripe);

                    tx = PdfMargin;
                    DrawCellText(c, tx, rowY, colW[0], rowH, f.Section, "F1", 9, ColorText, false); tx += colW[0];
                    DrawCellText(c, tx, rowY, colW[1], rowH, f.Field, "F1", 9, ColorText, false); tx += colW[1];
                    DrawCellText(c, tx, rowY, colW[2], rowH, $"£{f.StaffValue:F2}", "F3", 9, ColorText, true); tx += colW[2];
                    DrawCellText(c, tx, rowY, colW[3], rowH, $"£{f.ZReportValue:F2}", "F3", 9, ColorText, true); tx += colW[3];

                    var absVar = Math.Abs(f.Variance);
                    var vColor = absVar == 0 ? ColorGreen : absVar <= 5 ? ColorAmber : ColorRed;
                    var vText  = f.Variance > 0 ? $"+£{f.Variance:F2}" : $"£{f.Variance:F2}";
                    DrawCellText(c, tx, rowY, colW[4], rowH, vText, "F3", 9, vColor, true);
                }
            }

            var tableBottom = rowY;
            PdfStrokeRect(c, PdfMargin, tableBottom, contentW, tableTop - tableBottom, ColorBorder);
            tx = PdfMargin;
            for (var i = 0; i < colW.Length - 1; i++)
            {
                tx += colW[i];
                PdfLine(c, tx, tableTop, tx, tableBottom, ColorBorder);
            }

            // ── Footer ──
            var footerY = PdfMargin + 20;
            var committedBy = detail?.CommittedByName ?? summary.CommittedByName;
            var committedAt = detail?.CommittedAt ?? summary.CommittedAt;
            var adminBy     = detail?.AdminSubmittedByName ?? summary.AdminSubmittedByName;

            var footerParts = new List<string>();
            if (!string.IsNullOrEmpty(committedBy))
                footerParts.Add($"Committed by {committedBy}" + (committedAt.HasValue ? $" on {committedAt:dd MMM yyyy, HH:mm}" : ""));
            if (!string.IsNullOrEmpty(adminBy))
                footerParts.Add($"Reconciled by {adminBy}");

            PdfLine(c, PdfMargin, footerY + 14, PdfPageWidth - PdfMargin, footerY + 14, ColorBorder);
            if (footerParts.Count > 0)
                PdfText(c, PdfMargin, footerY, string.Join("   |   ", footerParts), "F1", 8, ColorFaint);
            PdfText(c, PdfPageWidth - PdfMargin - 150, footerY, $"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC", "F1", 7, ColorFaint);

            return AssembleSinglePagePdf(c.ToString());
        }

        private static void DrawStatCard(
            StringBuilder c, double x, double y, double w, double h,
            string label, string value, (double R, double G, double B) bg, (double R, double G, double B) valueColor)
        {
            PdfFillRect(c, x, y, w, h, bg);
            PdfStrokeRect(c, x, y, w, h, ColorBorder);
            PdfText(c, x + 10, y + h - 18, label, "F2", 7.5, ColorMuted);
            PdfText(c, x + 10, y + 12, value, "F2", 14, valueColor);
        }

        // alignRight positions text so it ends near the cell's right edge (for numeric
        // columns); otherwise it's left-padded from the cell's left edge.
        private static void DrawCellText(
            StringBuilder c, double cellX, double cellY, double cellW, double cellH,
            string text, string font, double size, (double R, double G, double B) color, bool alignRight)
        {
            const double pad = 8;
            var textY = cellY + (cellH - size) / 2 + 2;
            var textX = alignRight
                ? cellX + cellW - pad - EstimateTextWidth(text, font, size)
                : cellX + pad;
            PdfText(c, textX, textY, text, font, size, color);
        }

        // Courier is fixed-width at exactly 0.6em per the PDF base-14 font metrics;
        // Helvetica-Bold digits/most chars average close to 0.62em — close enough for
        // right-aligning short numeric/label strings without embedding real font metrics.
        private static double EstimateTextWidth(string text, string font, double size) =>
            text.Length * size * (font == "F3" ? 0.6 : 0.62);

        private static void PdfText(
            StringBuilder c, double x, double y, string text, string font, double size,
            (double R, double G, double B)? color = null)
        {
            var (r, g, b) = color ?? ColorText;
            c.AppendLine($"{Fmt(r)} {Fmt(g)} {Fmt(b)} rg");
            c.AppendLine($"BT /{font} {Fmt(size)} Tf {Fmt(x)} {Fmt(y)} Td ({EscapePdfText(text)}) Tj ET");
        }

        private static void PdfFillRect(StringBuilder c, double x, double y, double w, double h, (double R, double G, double B) color)
        {
            c.AppendLine($"{Fmt(color.R)} {Fmt(color.G)} {Fmt(color.B)} rg");
            c.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(w)} {Fmt(h)} re f");
        }

        private static void PdfStrokeRect(StringBuilder c, double x, double y, double w, double h, (double R, double G, double B) color, double lineWidth = 0.75)
        {
            c.AppendLine($"{Fmt(color.R)} {Fmt(color.G)} {Fmt(color.B)} RG");
            c.AppendLine($"{Fmt(lineWidth)} w");
            c.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(w)} {Fmt(h)} re S");
        }

        private static void PdfLine(StringBuilder c, double x1, double y1, double x2, double y2, (double R, double G, double B) color, double lineWidth = 0.5)
        {
            c.AppendLine($"{Fmt(color.R)} {Fmt(color.G)} {Fmt(color.B)} RG");
            c.AppendLine($"{Fmt(lineWidth)} w");
            c.AppendLine($"{Fmt(x1)} {Fmt(y1)} m {Fmt(x2)} {Fmt(y2)} l S");
        }

        private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // Assembles a single-page PDF from a content stream, declaring three base-14
        // fonts: F1 Helvetica (body), F2 Helvetica-Bold (headings/labels), F3 Courier
        // (fixed-width, for reliably right-aligned table figures).
        private static byte[] AssembleSinglePagePdf(string pageContent)
        {
            // Latin-1 (not ASCII) so £ (U+00A3) survives as a single byte (0xA3) — it
            // maps identically under WinAnsiEncoding, which the fonts below declare.
            // ASCII would silently mangle it to '?'.
            var contentBytes = Encoding.Latin1.GetBytes(pageContent);
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R /F3 7 0 R >> >> >>",
                $"<< /Length {contentBytes.Length} >>\nstream\n{pageContent}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>",
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

            return Encoding.Latin1.GetBytes(pdf.ToString());
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
