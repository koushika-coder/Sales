namespace Sales.DTOs;

// ============ SHOP SALE DTOs ============
public class CreateShopSaleRequest
{
    public string ProductDetails { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class UpdateShopSaleRequest
{
    public string? ProductDetails { get; set; }
    public int? Quantity { get; set; }
    public decimal? Amount { get; set; }
}

public class ImportShopSaleFromGmailRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}

public class ImportShopSaleFromGmailSearchRequest
{
    public string AccessToken { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SubjectKeyword { get; set; } = string.Empty;
    public string? GmailQuery { get; set; }
    public int MaxResults { get; set; } = 10;
}

public class ShopSaleDto
{
    public int ShopSaleId { get; set; }
    public int UserId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string ProductDetails { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============ CREDIT CARD BANKING DTOs ============
public class CreateCreditCardBankingRequest
{
    public string CardLast4Digits { get; set; } = string.Empty;
    public string CardholderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal Paypoint { get; set; } 
    public decimal EFTAmount { get; set; }
}

public class UpdateCreditCardBankingRequest
{
    public string? CardLast4Digits { get; set; }
    public string? CardholderName { get; set; }
    public string? BankName { get; set; }
    public decimal? Amount { get; set; }
}

public class CreditCardBankingDto
{
    public int CreditCardBankingId { get; set; }
    public int UserId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string CardLast4Digits { get; set; } = string.Empty;
    public string CardholderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============ CASH BANKING DTOs ============
public class CreateCashBankingRequest
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Denomination { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class UpdateCashBankingRequest
{
    public string? ReceiptNumber { get; set; }
    public string? Denomination { get; set; }
    public string? BankName { get; set; }
    public decimal? Amount { get; set; }
}

    //public class CashBankingDto
    //{
    //    public int CashBankingId { get; set; }
    //    public int UserId { get; set; }
    //    public string TransactionNumber { get; set; } = string.Empty;
    //    public DateTime TransactionDate { get; set; }
    //    public decimal Amount { get; set; }
    //    public string ReceiptNumber { get; set; } = string.Empty;
    //    public string Denomination { get; set; } = string.Empty;
    //    public string BankName { get; set; } = string.Empty;
    //    public string Status { get; set; } = string.Empty;
    //    public DateTime CreatedAt { get; set; }
    //    public DateTime UpdatedAt { get; set; }
    //}

// ============ DEDUCTION DTOs ============
public class CreateDeductionRequest
{
    public string DeductionType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class UpdateDeductionRequest
{
    public string? DeductionType { get; set; }
    public string? Reason { get; set; }
    public decimal? Amount { get; set; }
}

//public class DeductionDto
//{
//    public int DeductionId { get; set; }
//    public int UserId { get; set; }
//    public string TransactionNumber { get; set; } = string.Empty;
//    public DateTime TransactionDate { get; set; }
//    public decimal Amount { get; set; }
//    public string DeductionType { get; set; } = string.Empty;
//    public string Reason { get; set; } = string.Empty;
//    public string Status { get; set; } = string.Empty;
//    public DateTime CreatedAt { get; set; }
//    public DateTime UpdatedAt { get; set; }
//}

// ============ INSTANT LOTTERY DTOs ============
public class CreateInstantLotteryRequest
{
    public decimal TicketPrice { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public class UpdateInstantLotteryRequest
{
    public decimal? TicketPrice { get; set; }
    public decimal? PrizeAmount { get; set; }
    public string? Status { get; set; }
}

public class InstantLotteryDto
{
    public int InstantLotteryId { get; set; }
    public int UserId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal TicketPrice { get; set; }
    public decimal? PrizeAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsWinner { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============ LOTTERY DTOs ============
public class CreateLotteryRequest
{
    public string LotteryName { get; set; } = string.Empty;
    public decimal TicketCost { get; set; }
    public DateTime DrawDate { get; set; }
}

public class UpdateLotteryRequest
{
    public string? LotteryName { get; set; }
    public decimal? TicketCost { get; set; }
    public decimal? PrizeAmount { get; set; }
    public string? Status { get; set; }
}

public class LotteryDto
{
    public int LotteryId { get; set; }
    public int UserId { get; set; }
    public string LotteryName { get; set; } = string.Empty;
    public string TicketNumber { get; set; } = string.Empty;
    public DateTime DrawDate { get; set; }
    public decimal TicketCost { get; set; }
    public decimal? PrizeAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsWinner { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============ RECONCILIATION DTOs ============
public class CreateReconciliationRequest
{
    public DateTime ReconciliationDate { get; set; }
    public string? Notes { get; set; }
}

public class ReconciliationDto
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
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ============ SUMMARY DTOs ============
public class SummaryDto
{
    public decimal TotalShopSales { get; set; }
    public decimal TotalCreditCardTransactions { get; set; }
    public decimal TotalCashTransactions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalLotteryAmount { get; set; }
    public int TotalLotteryWins { get; set; }
    public decimal NetAmount { get; set; }
    public DateTime SummaryDate { get; set; }
    public int PendingTransactions { get; set; }
    public int CommittedTransactions { get; set; }
}

// ============ GOOGLE OAUTH DTOs ============
public class GoogleAuthorizationRequest
{
    public string AuthCode { get; set; } = string.Empty;
}

public class GoogleTokenResponse
{
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public string? token_type { get; set; }
}

public class GoogleOAuthCallbackResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Message { get; set; } = "Gmail authorization successful";
}

public class GmailImportRequest
{
    public string AuthCode { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}

public class GmailSearchRequest
{
    public string AuthCode { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SubjectKeyword { get; set; } = string.Empty;
    public string? GmailQuery { get; set; }
    public int MaxResults { get; set; } = 10;
}
