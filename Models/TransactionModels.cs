namespace Sales.Models;

public class ShopSale
{
    public int ShopSaleId { get; set; }
    public int UserId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string ProductDetails { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = "pending"; // pending, committed, cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

//public class CreditCardBanking
//{
//    public int CreditCardBankingId { get; set; }
//    public int UserId { get; set; }
//    public string TransactionNumber { get; set; } = string.Empty;
//    public DateTime TransactionDate { get; set; }
//    public decimal Amount { get; set; }
//    public string CardLast4Digits { get; set; } = string.Empty;
//    public string CardholderName { get; set; } = string.Empty;
//    public string BankName { get; set; } = string.Empty;
//    public string ReferenceNumber { get; set; } = string.Empty;
//    public string Status { get; set; } = "pending"; // pending, processed, failed, cancelled
//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

//    public User? User { get; set; }
//}

//public class CashBanking
//{
//    public int CashBankingId { get; set; }
//    public int UserId { get; set; }
//    public string TransactionNumber { get; set; } = string.Empty;
//    public DateTime TransactionDate { get; set; }
//    public decimal Amount { get; set; }
//    public string ReceiptNumber { get; set; } = string.Empty;
//    public string Denomination { get; set; } = string.Empty; // e.g., "100x5, 50x3, 20x2"
//    public string BankName { get; set; } = string.Empty;
//    public string Status { get; set; } = "pending"; // pending, deposited, cancelled
//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

////    public User? User { get; set; }
////}

//public class Deduction
//{
//    public int DeductionId { get; set; }
//    public int UserId { get; set; }
//    public string TransactionNumber { get; set; } = string.Empty;
//    public DateTime TransactionDate { get; set; }
//    public decimal Amount { get; set; }
//    public string DeductionType { get; set; } = string.Empty; // e.g., "discount", "fee", "tax", "allowance"
//    public string Reason { get; set; } = string.Empty;
//    public string Status { get; set; } = "pending"; // pending, applied, cancelled
//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

//    public User? User { get; set; }
//}

//public class InstantLottery
//{
//    public int InstantLotteryId { get; set; }
//    public int UserId { get; set; }
//    public string TicketNumber { get; set; } = string.Empty;
//    public DateTime PurchaseDate { get; set; }
//    public decimal TicketPrice { get; set; }
//    public decimal? PrizeAmount { get; set; }
//    public string Status { get; set; } = "active"; // active, won, lost, cancelled, redeemed
//    public bool IsWinner { get; set; } = false;
//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

//    public User? User { get; set; }
//}

//public class Lottery
//{
//    public int LotteryId { get; set; }
//    public int UserId { get; set; }
//    public string LotteryName { get; set; } = string.Empty;
//    public string TicketNumber { get; set; } = string.Empty;
//    public DateTime DrawDate { get; set; }
//    public decimal TicketCost { get; set; }
//    public decimal? PrizeAmount { get; set; }
//    public string Status { get; set; } = "pending"; // pending, drawn, won, lost, cancelled
//    public bool IsWinner { get; set; } = false;
//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

//    public User? User { get; set; }
//}

public class Reconciliation
{
    public int ReconciliationId { get; set; }
    public int UserId { get; set; }
    public DateTime ReconciliationDate { get; set; }
    public decimal TotalShopSales { get; set; }
    public decimal TotalCreditCardTransactions { get; set; }
    public decimal TotalCashTransactions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalLotteryAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = "draft"; // draft, finalized
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}

public class TransactionAudit
{
    public int AuditId { get; set; }
    public int UserId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // ShopSale, CreditCardBanking, etc.
    public int TransactionId { get; set; }
    public string Action { get; set; } = string.Empty; // Created, Updated, Committed, Deleted
    public string OldValues { get; set; } = string.Empty; // JSON
    public string NewValues { get; set; } = string.Empty; // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
