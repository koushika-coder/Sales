namespace Sales.Models
{
    public class AdminReconciliation
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }

        // Credit Card
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }

        // Cash (SafeDrop)
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

        // Instant Lottery
        public int InstantLotteryTotalCount { get; set; }
        public decimal InstantLotteryTotalSales { get; set; }

        // Lottery / Paypoint
        public decimal LotteryValue { get; set; }
        public decimal PaypointValue { get; set; }

        // Totals from SummaryCommit
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }

        // Admin review
        public string Status { get; set; } = "pending"; // "pending" | "submitted"
        public string? AdminNotes { get; set; }
        public int? SubmittedByAdminId { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
