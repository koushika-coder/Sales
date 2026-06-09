using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services;

// ============ CREDIT CARD BANKING SERVICE ============
public interface ICreditCardBankingService
{
    //Task<CreditCardBankingDto?> CreateAsync(int userId, CreateCreditCardBankingRequest request);
    //Task<CreditCardBankingDto?> GetByIdAsync(int creditCardBankingId);
    //Task<PaginatedResponse<CreditCardBankingDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);
    //Task<CreditCardBankingDto?> UpdateAsync(int creditCardBankingId, UpdateCreditCardBankingRequest request);
    //Task<bool> CommitAsync(int creditCardBankingId);
    //Task<bool> DeleteAsync(int creditCardBankingId);
}

//public class CreditCardBankingService : ICreditCardBankingService
//{
//    private readonly SalesDbContext _context;

//    public CreditCardBankingService(SalesDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<CreditCardBankingDto?> CreateAsync(int userId, CreateCreditCardBankingRequest request)
//    {
//        var transaction = new CreditCardBanking
//        {
//            UserId = userId,
//            TransactionNumber = GenerateTransactionNumber("CC"),
//            TransactionDate = request.TransactionDate,
//            Amount = request.Amount,
//            CardLast4Digits = request.CardLast4Digits,
//            CardholderName = request.CardholderName,
//            BankName = request.BankName,
//            ReferenceNumber = GenerateReferenceNumber(),
//            Status = "pending"
//        };

//        _context.CreditCardBankings.Add(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "CreditCardBanking", transaction.CreditCardBankingId, "Created", null, transaction);

//        return MapToDto(transaction);
//    }

//    public async Task<CreditCardBankingDto?> GetByIdAsync(int creditCardBankingId)
//    {
//        var transaction = await _context.CreditCardBankings.FirstOrDefaultAsync(c => c.CreditCardBankingId == creditCardBankingId);
//        return transaction != null ? MapToDto(transaction) : null;
//    }

//    public async Task<PaginatedResponse<CreditCardBankingDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.CreditCardBankings.Where(c => c.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var transactions = await query
//            .OrderByDescending(c => c.CreatedAt)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<CreditCardBankingDto>
//        {
//            Items = transactions.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<CreditCardBankingDto?> UpdateAsync(int creditCardBankingId, UpdateCreditCardBankingRequest request)
//    {
//        var transaction = await _context.CreditCardBankings.FirstOrDefaultAsync(c => c.CreditCardBankingId == creditCardBankingId);
//        if (transaction == null || transaction.Status != "pending")
//            return null;

//        var oldTransaction = new CreditCardBanking
//        {
//            CardLast4Digits = transaction.CardLast4Digits,
//            Amount = transaction.Amount,
//            BankName = transaction.BankName
//        };

//        if (!string.IsNullOrEmpty(request.CardLast4Digits))
//            transaction.CardLast4Digits = request.CardLast4Digits;
//        if (!string.IsNullOrEmpty(request.CardholderName))
//            transaction.CardholderName = request.CardholderName;
//        if (!string.IsNullOrEmpty(request.BankName))
//            transaction.BankName = request.BankName;
//        if (request.Amount.HasValue && request.Amount > 0)
//            transaction.Amount = request.Amount.Value;

//        transaction.UpdatedAt = DateTime.UtcNow;
//        _context.CreditCardBankings.Update(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CreditCardBanking", creditCardBankingId, "Updated", oldTransaction, transaction);

//        return MapToDto(transaction);
//    }

//    public async Task<bool> CommitAsync(int creditCardBankingId)
//    {
//        var transaction = await _context.CreditCardBankings.FirstOrDefaultAsync(c => c.CreditCardBankingId == creditCardBankingId);
//        if (transaction == null || transaction.Status != "pending")
//            return false;

//        transaction.Status = "processed";
//        transaction.UpdatedAt = DateTime.UtcNow;
//        _context.CreditCardBankings.Update(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CreditCardBanking", creditCardBankingId, "Committed", null, transaction);

//        return true;
//    }

//    public async Task<bool> DeleteAsync(int creditCardBankingId)
//    {
//        var transaction = await _context.CreditCardBankings.FirstOrDefaultAsync(c => c.CreditCardBankingId == creditCardBankingId && c.Status == "pending");
//        if (transaction == null)
//            return false;

//        _context.CreditCardBankings.Remove(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CreditCardBanking", creditCardBankingId, "Deleted", transaction, null);

//        return true;
//    }

//    private static CreditCardBankingDto MapToDto(CreditCardBanking transaction)
//    {
//        return new CreditCardBankingDto
//        {
//            CreditCardBankingId = transaction.CreditCardBankingId,
//            UserId = transaction.UserId,
//            TransactionNumber = transaction.TransactionNumber,
//            TransactionDate = transaction.TransactionDate,
//            Amount = transaction.Amount,
//            CardLast4Digits = transaction.CardLast4Digits,
//            CardholderName = transaction.CardholderName,
//            BankName = transaction.BankName,
//            ReferenceNumber = transaction.ReferenceNumber,
//            Status = transaction.Status,
//            CreatedAt = transaction.CreatedAt,
//            UpdatedAt = transaction.UpdatedAt
//        };
//    }

//    private string GenerateTransactionNumber(string prefix)
//    {
//        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
//    }

//    private string GenerateReferenceNumber()
//    {
//        return $"REF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 12).ToUpper()}";
//    }

//    private async Task LogAudit(int userId, string transactionType, int transactionId, string action, object? oldValues, object? newValues)
//    {
//        var audit = new TransactionAudit
//        {
//            UserId = userId,
//            TransactionType = transactionType,
//            TransactionId = transactionId,
//            Action = action,
//            OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : string.Empty,
//            NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : string.Empty
//        };

//        _context.TransactionAudits.Add(audit);
//        await _context.SaveChangesAsync();
//    }
//}

// ============ CASH BANKING SERVICE ============
//public interface ICashBankingService
//{
//    Task<CashBankingDto?> CreateAsync(int userId, CreateCashBankingRequest request);
//    Task<CashBankingDto?> GetByIdAsync(int cashBankingId);
//    Task<PaginatedResponse<CashBankingDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);
//    Task<CashBankingDto?> UpdateAsync(int cashBankingId, UpdateCashBankingRequest request);
//    Task<bool> CommitAsync(int cashBankingId);
//    Task<bool> DeleteAsync(int cashBankingId);
//}

//public class CashBankingService : ICashBankingService
//{
//    private readonly SalesDbContext _context;

//    //public CashBankingService(SalesDbContext context)
//    //{
//    //    _context = context;
//    //}

//    public async Task<CashBankingDto?> CreateAsync(int userId, CreateCashBankingRequest request)
//    {
//        var transaction = new CashBanking
//        {
//            UserId = userId,
//            TransactionNumber = GenerateTransactionNumber("CB"),
//            TransactionDate = request.TransactionDate,
//            Amount = request.Amount,
//            ReceiptNumber = request.ReceiptNumber,
//            Denomination = request.Denomination,
//            BankName = request.BankName,
//            Status = "pending"
//        };

//        _context.CashBankings.Add(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "CashBanking", transaction.CashBankingId, "Created", null, transaction);

//        return MapToDto(transaction);
//    }

//    public async Task<CashBankingDto?> GetByIdAsync(int cashBankingId)
//    {
//        var transaction = await _context.CashBankings.FirstOrDefaultAsync(c => c.CashBankingId == cashBankingId);
//        return transaction != null ? MapToDto(transaction) : null;
//    }

//    public async Task<PaginatedResponse<CashBankingDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.CashBankings.Where(c => c.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var transactions = await query
//            .OrderByDescending(c => c.CreatedAt)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<CashBankingDto>
//        {
//            Items = transactions.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<CashBankingDto?> UpdateAsync(int cashBankingId, UpdateCashBankingRequest request)
//    {
//        var transaction = await _context.CashBankings.FirstOrDefaultAsync(c => c.CashBankingId == cashBankingId);
//        if (transaction == null || transaction.Status != "pending")
//            return null;

//        if (!string.IsNullOrEmpty(request.ReceiptNumber))
//            transaction.ReceiptNumber = request.ReceiptNumber;
//        if (!string.IsNullOrEmpty(request.Denomination))
//            transaction.Denomination = request.Denomination;
//        if (!string.IsNullOrEmpty(request.BankName))
//            transaction.BankName = request.BankName;
//        if (request.Amount.HasValue && request.Amount > 0)
//            transaction.Amount = request.Amount.Value;

//        transaction.UpdatedAt = DateTime.UtcNow;
//        _context.CashBankings.Update(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CashBanking", cashBankingId, "Updated", null, transaction);

//        return MapToDto(transaction);
//    }

//    public async Task<bool> CommitAsync(int cashBankingId)
//    {
//        var transaction = await _context.CashBankings.FirstOrDefaultAsync(c => c.CashBankingId == cashBankingId);
//        if (transaction == null || transaction.Status != "pending")
//            return false;

//        transaction.Status = "deposited";
//        transaction.UpdatedAt = DateTime.UtcNow;
//        _context.CashBankings.Update(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CashBanking", cashBankingId, "Committed", null, transaction);

//        return true;
//    }

//    public async Task<bool> DeleteAsync(int cashBankingId)
//    {
//        var transaction = await _context.CashBankings.FirstOrDefaultAsync(c => c.CashBankingId == cashBankingId && c.Status == "pending");
//        if (transaction == null)
//            return false;

//        _context.CashBankings.Remove(transaction);
//        await _context.SaveChangesAsync();

//        await LogAudit(transaction.UserId, "CashBanking", cashBankingId, "Deleted", transaction, null);

//        return true;
//    }

//    private static CashBankingDto MapToDto(CashBanking transaction)
//    {
//        return new CashBankingDto
//        {
//            CashBankingId = transaction.CashBankingId,
//            UserId = transaction.UserId,
//            TransactionNumber = transaction.TransactionNumber,
//            TransactionDate = transaction.TransactionDate,
//            Amount = transaction.Amount,
//            ReceiptNumber = transaction.ReceiptNumber,
//            Denomination = transaction.Denomination,
//            BankName = transaction.BankName,
//            Status = transaction.Status,
//            CreatedAt = transaction.CreatedAt,
//            UpdatedAt = transaction.UpdatedAt
//        };
//    }

//    private string GenerateTransactionNumber(string prefix)
//    {
//        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
//    }

//    private async Task LogAudit(int userId, string transactionType, int transactionId, string action, object? oldValues, object? newValues)
//    {
//        var audit = new TransactionAudit
//        {
//            UserId = userId,
//            TransactionType = transactionType,
//            TransactionId = transactionId,
//            Action = action,
//            OldValues = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : string.Empty,
//            NewValues = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : string.Empty
//        };

//        _context.TransactionAudits.Add(audit);
//        await _context.SaveChangesAsync();
//    }
//}
