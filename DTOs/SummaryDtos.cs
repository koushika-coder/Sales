namespace Sales.DTOs
{
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

        // Cash Banking (SafeDrop)
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash { get; set; }   // read-only: LastSafe + SafeDropAmount

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }

        // Instant Lottery Inventory — computed sum, read-only
        public decimal InstantLotteryTotalSales { get; set; }

        // Lottery Management
        public decimal LotteryValue { get; set; }

        // Paypoint Management
        public decimal PaypointValue { get; set; }

        // Commit status
        public bool IsCommitted { get; set; }
        public DateTime? CommittedAt { get; set; }
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
        public decimal UserTotal { get; set; }         // Cash + Card + ManualCard
        public decimal ZReportGrandTotal { get; set; } // GRAND TOTAL from Z-report
        public decimal TotalDifference { get; set; }   // |UserTotal - ZReportGrandTotal|
        public bool CanCommit { get; set; }            // TotalDifference <= £5
        public List<ZReportFieldComparison> Fields { get; set; } = new();
    }

    public class SummaryUpdateRequest
    {
        // Credit Card Banking — send all entries; id=0 creates a new row
        public List<CreditCardSummaryUpdateEntry> CreditCardEntries { get; set; } = new();

        // Cash Banking — updates SafeDrop.LastSafe and SafeDrop.SafeDropAmount
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }

        // Deductions
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }

        // Lottery Management
        public decimal LotteryValue { get; set; }

        // Paypoint Management
        public decimal PaypointValue { get; set; }
    }
}
