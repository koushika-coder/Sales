namespace Sales.DTOs
{
    // ── Admin Reconciliation DTOs ─────────────────────────────────────────────

    public class PendingReconciliationResponse
    {
        public bool HasPending { get; set; }
        public int Id { get; set; }
        public DateOnly Date { get; set; }

        // Credit Card
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }

        // Cash
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash => LastSafe + SafeDropAmount;

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }
        public decimal SupplierInvoicesTotal { get; set; }

        // Instant Lottery
        public int InstantLotteryTotalCount { get; set; }
        public decimal InstantLotteryTotalSales { get; set; }

        // Lottery / Paypoint
        public decimal LotteryValue { get; set; }
        public decimal PaypointValue { get; set; }

        // Totals
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    // Date-picker list: one row per committed date
    public class CommittedSummaryListItem
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }
        public DateTime CommittedAt { get; set; }
    }

    // Full breakdown for a single committed date
    public class CommittedSummaryDetailResponse
    {
        public int CommitId { get; set; }
        public DateOnly Date { get; set; }

        // Credit Card
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }

        // Cash
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash => LastSafe + SafeDropAmount;

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }
        public decimal SupplierInvoicesTotal { get; set; }

        // Instant Lottery
        public int InstantLotteryTotalCount { get; set; }
        public decimal InstantLotteryTotalSales { get; set; }

        // Lottery / Paypoint
        public decimal LotteryValue { get; set; }
        public decimal PaypointValue { get; set; }

        // Totals
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }

        public DateTime CommittedAt { get; set; }
    }

    public class AdminSubmitReconciliationRequest
    {
        public DateOnly Date { get; set; }   // which day is being submitted

        // Credit Card
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }

        // Cash
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }
        public decimal SupplierInvoicesTotal { get; set; }

        // Lottery / Paypoint
        public decimal LotteryValue { get; set; }
        public decimal PaypointValue { get; set; }

        // Totals
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }

        public string? AdminNotes { get; set; }
    }

    public class ReconciliationPortalResponse
    {
        public bool HasReconciliation { get; set; }
        public int Id { get; set; }
        public DateOnly Date { get; set; }

        // Credit Card
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }

        // Cash
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash => LastSafe + SafeDropAmount;

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }
        public decimal SupplierInvoicesTotal { get; set; }

        // Instant Lottery
        public int InstantLotteryTotalCount { get; set; }
        public decimal InstantLotteryTotalSales { get; set; }

        // Lottery / Paypoint
        public decimal LotteryValue { get; set; }
        public decimal PaypointValue { get; set; }

        // Totals
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }

        public string? AdminNotes { get; set; }
        public DateTime SubmittedAt { get; set; }
    }


    public class CreditCardSummaryEntry
    {
        public int Id { get; set; }
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreditCardSummaryUpdateEntry
    {
        public int Id { get; set; }   // 0 = new record
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }
    }

    public class SummaryResponse
    {
        public DateOnly Date { get; set; }

        // Credit Card Banking — full list for today
        public List<CreditCardSummaryEntry> CreditCardEntries { get; set; } = new();

        // Cash Banking (SafeDrop) — null means no SafeDrop record exists yet for this date
        // (as opposed to a genuinely saved 0), so the frontend can show a blank field.
        public decimal? LastSafe { get; set; }
        public decimal? SafeDropAmount { get; set; }
        public decimal? Cash { get; set; }   // read-only: LastSafe + SafeDropAmount

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }

        // Instant Lottery Inventory — computed sums, read-only
        public int InstantLotteryTotalCount { get; set; }   // total scratch cards sold
        public decimal InstantLotteryTotalSales { get; set; } // total £ value sold

        // Supplier Payout — computed live sum of that day's Supplier Invoices, read-only
        // (the underlying invoices are entered on the Deductions page).
        public decimal SupplierInvoicesTotal { get; set; }

        // Lottery Management
        public decimal LotteryValue { get; set; }

        // Paypoint Management
        public decimal PaypointValue { get; set; }

        // Commit status
        public bool IsCommitted { get; set; }
        public DateTime? CommittedAt { get; set; }

        // True when the active date's difference exceeded £5.00 and is locked awaiting
        // admin review — data entry should be read-only until an admin resolves it.
        public bool IsPendingAdminReview { get; set; }

        // True when at least one record exists for the active date (or an uncommitted fallback).
        // False means the active date is fresh — no data entered yet.
        public bool HasTodayData { get; set; }
    }

    public class SummaryCommitRequest
    {
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }
    }

    public class SummaryCommitResponse
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }
        public DateTime CommittedAt { get; set; }
        // The next active date's summary — frontend should replace the dashboard with this
        public SummaryResponse NewSummary { get; set; } = null!;
    }

    public class ZReportFieldComparison
    {
        public string Section { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public decimal UserValue { get; set; }
        public decimal ZReportValue { get; set; }
        public decimal Difference { get; set; }
    }

    public class ZReportComparisonResponse
    {
        public DateOnly Date { get; set; }
        public decimal UserTotal { get; set; }         // Sum of all user-entered values
        public decimal ZReportGrandTotal { get; set; } // GRAND TOTAL from Z-report
        public decimal TotalDifference { get; set; }   // |UserTotal - ZReportGrandTotal|
        public bool CanCommit { get; set; }            // TotalDifference <= £5
        public List<ZReportFieldComparison> Fields { get; set; } = new();
    }

    public class ZReportEmailResult
    {
        public bool IsCommitted { get; set; }
        public bool IsPendingAdminReview { get; set; }
        public DateOnly TargetDate { get; set; }
        public string? Message { get; set; }
        public GmailMessageResponse? Email { get; set; }
    }

    public class SummaryUpdateRequest
    {
        // Credit Card Banking — send all entries; id=0 creates a new row
        public List<CreditCardSummaryUpdateEntry> CreditCardEntries { get; set; } = new();

        // Cash Banking — both entered by the user
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }

        // Lottery Management
        public decimal LotteryValue { get; set; }

        // Paypoint Management
        public decimal PaypointValue { get; set; }
    }
}
