namespace Sales.DTOs
{
    // One row in the date-wise report list
    public class ReportListItem
    {
        public DateOnly Date { get; set; }

        // Totals (from admin reconciliation if available, otherwise staff commit)
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Variance { get; set; }
        public bool WithinThreshold { get; set; }   // variance <= £5

        // Source flags
        public bool IsStaffCommitted { get; set; }
        public bool IsAdminReconciled { get; set; }

        // Staff commit info (null when admin-only)
        public int? CommittedByUserId { get; set; }
        public string? CommittedByName { get; set; }
        public DateTime? CommittedAt { get; set; }

        // Admin submission info (null if not admin-submitted)
        public int? AdminSubmittedByAdminId { get; set; }
        public string? AdminSubmittedByName { get; set; }
        public DateTime? AdminSubmittedAt { get; set; }
    }

    // Per-field comparison row in the detail report
    public class ReportFieldRow
    {
        public string Section { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public decimal StaffValue { get; set; }
        public decimal ZReportValue { get; set; }
        public decimal Variance { get; set; }
    }

    // Full detail for a single date
    public class ReportDetailResponse
    {
        public DateOnly Date { get; set; }

        // Per-field breakdown
        public List<ReportFieldRow> Fields { get; set; } = new();

        // Overall totals
        public decimal StaffTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal TotalVariance { get; set; }
        public bool WithinThreshold { get; set; }

        // Who committed (staff) — null when admin-only reconciliation
        public int? CommittedByUserId { get; set; }
        public string? CommittedByName { get; set; }
        public DateTime? CommittedAt { get; set; }

        // Who submitted (admin) — null if not yet admin-submitted
        public int? AdminSubmittedByAdminId { get; set; }
        public string? AdminSubmittedByName { get; set; }
        public DateTime? AdminSubmittedAt { get; set; }

        // False when Gmail fetch failed — Z-Report values will all be 0
        public bool ZReportAvailable { get; set; }
    }
}
