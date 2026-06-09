using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services;

// ============ RECONCILIATION SERVICE ============
public interface IReconciliationService
{
    Task<SummaryDto> GetDailySummaryAsync(int userId, DateTime date);
    Task<ReconciliationDto?> CreateReconciliationAsync(int userId, CreateReconciliationRequest request);
    Task<ReconciliationDto?> GetByIdAsync(int reconciliationId);
    Task<PaginatedResponse<ReconciliationDto>> GetUserReconciliationsAsync(int userId, int pageNumber = 1, int pageSize = 10);
    Task<bool> FinalizeReconciliationAsync(int reconciliationId);
    Task<bool> CommitAllPendingTransactionsAsync(int userId);
}

//public class ReconciliationService : IReconciliationService
//{
//    private readonly SalesDbContext _context;

//    public ReconciliationService(SalesDbContext context)
//    {
//        _context = context;
//    }

//    //public async Task<SummaryDto> GetDailySummaryAsync(int userId, DateTime date)
//    //{
//    //    // Get start and end of day
//    //    var startOfDay = date.Date;
//    //    var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

//    //    // Shop Sales
//    //    var shopSalesToday = await _context.ShopSales
//    //        .Where(s => s.UserId == userId && s.CreatedAt >= startOfDay && s.CreatedAt <= endOfDay)
//    //        .SumAsync(s => (decimal?)s.Amount) ?? 0;

//    //    // Credit Card Transactions
//    //    var creditCardToday = await _context.CreditCardBankings
//    //        .Where(c => c.UserId == userId && c.CreatedAt >= startOfDay && c.CreatedAt <= endOfDay)
//    //        .SumAsync(c => (decimal?)c.Amount) ?? 0;

//    //    // Cash Banking
//    //    var cashToday = await _context.CashBankings
//    //        .Where(c => c.UserId == userId && c.CreatedAt >= startOfDay && c.CreatedAt <= endOfDay)
//    //        .SumAsync(c => (decimal?)c.Amount) ?? 0;

//    //    // Deductions
//    //    var deductionsToday = await _context.Deductions
//    //        .Where(d => d.UserId == userId && d.CreatedAt >= startOfDay && d.CreatedAt <= endOfDay)
//    //        .SumAsync(d => (decimal?)d.Amount) ?? 0;

//    //    // Lottery Prizes (only won amounts)
//    //    var lotteryWinsToday = await _context.Lotteries
//    //        .Where(l => l.UserId == userId && l.IsWinner && l.CreatedAt >= startOfDay && l.CreatedAt <= endOfDay)
//    //        .SumAsync(l => (decimal?)(l.PrizeAmount ?? 0)) ?? 0;

//    //    var instantLotteryWinsToday = await _context.InstantLotteries
//    //        .Where(l => l.UserId == userId && l.IsWinner && l.CreatedAt >= startOfDay && l.CreatedAt <= endOfDay)
//    //        .SumAsync(l => (decimal?)(l.PrizeAmount ?? 0)) ?? 0;

//    //    var totalLotteryAmount = lotteryWinsToday + instantLotteryWinsToday;

//    //    // Count transactions by status
//    //    var pendingCount = await _context.ShopSales
//    //        .Where(s => s.UserId == userId && s.Status == "pending")
//    //        .CountAsync();
//    //    pendingCount += await _context.CreditCardBankings
//    //        .Where(c => c.UserId == userId && c.Status == "pending")
//    //        .CountAsync();
//    //    pendingCount += await _context.CashBankings
//    //        .Where(c => c.UserId == userId && c.Status == "pending")
//    //        .CountAsync();

//    //    var committedCount = await _context.ShopSales
//    //        .Where(s => s.UserId == userId && s.Status == "committed")
//    //        .CountAsync();
//    //    committedCount += await _context.CreditCardBankings
//    //        .Where(c => c.UserId == userId && c.Status == "processed")
//    //        .CountAsync();
//    //    committedCount += await _context.CashBankings
//    //        .Where(c => c.UserId == userId && c.Status == "deposited")
//    //        .CountAsync();

//    //    var lotteryWins = await _context.Lotteries
//    //        .Where(l => l.UserId == userId && l.IsWinner)
//    //        .CountAsync();
//    //    lotteryWins += await _context.InstantLotteries
//    //        .Where(l => l.UserId == userId && l.IsWinner)
//    //        .CountAsync();

//    //    var netAmount = shopSalesToday + creditCardToday + cashToday + totalLotteryAmount - deductionsToday;

//    //    return new SummaryDto
//    //    {
//    //        TotalShopSales = shopSalesToday,
//    //        TotalCreditCardTransactions = creditCardToday,
//    //        TotalCashTransactions = cashToday,
//    //        TotalDeductions = deductionsToday,
//    //        TotalLotteryAmount = totalLotteryAmount,
//    //        TotalLotteryWins = lotteryWins,
//    //        NetAmount = netAmount,
//    //        SummaryDate = date,
//    //        PendingTransactions = pendingCount,
//    //        CommittedTransactions = committedCount
//    //    };
//    //}

//    public async Task<ReconciliationDto?> CreateReconciliationAsync(int userId, CreateReconciliationRequest request)
//    {
//        var summary = await GetDailySummaryAsync(userId, request.ReconciliationDate);

//        var reconciliation = new Reconciliation
//        {
//            UserId = userId,
//            ReconciliationDate = request.ReconciliationDate,
//            TotalShopSales = summary.TotalShopSales,
//            TotalCreditCardTransactions = summary.TotalCreditCardTransactions,
//            TotalCashTransactions = summary.TotalCashTransactions,
//            TotalDeductions = summary.TotalDeductions,
//            TotalLotteryAmount = summary.TotalLotteryAmount,
//            NetAmount = summary.NetAmount,
//            Status = "draft",
//            Notes = request.Notes ?? string.Empty
//        };

//        _context.Reconciliations.Add(reconciliation);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "Reconciliation", reconciliation.ReconciliationId, "Created", null, reconciliation);

//        return MapToDto(reconciliation);
//    }

//    public async Task<ReconciliationDto?> GetByIdAsync(int reconciliationId)
//    {
//        var reconciliation = await _context.Reconciliations.FirstOrDefaultAsync(r => r.ReconciliationId == reconciliationId);
//        return reconciliation != null ? MapToDto(reconciliation) : null;
//    }

//    public async Task<PaginatedResponse<ReconciliationDto>> GetUserReconciliationsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.Reconciliations.Where(r => r.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var reconciliations = await query
//            .OrderByDescending(r => r.ReconciliationDate)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<ReconciliationDto>
//        {
//            Items = reconciliations.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<bool> FinalizeReconciliationAsync(int reconciliationId)
//    {
//        var reconciliation = await _context.Reconciliations.FirstOrDefaultAsync(r => r.ReconciliationId == reconciliationId);
//        if (reconciliation == null || reconciliation.Status != "draft")
//            return false;

//        reconciliation.Status = "finalized";
//        reconciliation.UpdatedAt = DateTime.UtcNow;
//        _context.Reconciliations.Update(reconciliation);
//        await _context.SaveChangesAsync();

//        await LogAudit(reconciliation.UserId, "Reconciliation", reconciliationId, "Finalized", null, reconciliation);

//        return true;
//    }

//    //public async Task<bool> CommitAllPendingTransactionsAsync(int userId)
//    //{
//    //    // Commit all pending Shop Sales
//    //    var pendingShopSales = await _context.ShopSales
//    //        .Where(s => s.UserId == userId && s.Status == "pending")
//    //        .ToListAsync();

//    //    foreach (var sale in pendingShopSales)
//    //    {
//    //        sale.Status = "committed";
//    //        sale.UpdatedAt = DateTime.UtcNow;
//    //    }

//    //    // Process all pending Credit Card transactions
//    //    var pendingCreditCards = await _context.CreditCardBankings
//    //        .Where(c => c.UserId == userId && c.Status == "pending")
//    //        .ToListAsync();

//    //    foreach (var card in pendingCreditCards)
//    //    {
//    //        card.Status = "processed";
//    //        card.UpdatedAt = DateTime.UtcNow;
//    //    }

//    //    // Deposit all pending Cash Banking
//    //    var pendingCash = await _context.CashBankings
//    //        .Where(c => c.UserId == userId && c.Status == "pending")
//    //        .ToListAsync();

//    //    foreach (var cash in pendingCash)
//    //    {
//    //        cash.Status = "deposited";
//    //        cash.UpdatedAt = DateTime.UtcNow;
//    //    }

//    //    // Apply all pending Deductions
//    //    var pendingDeductions = await _context.Deductions
//    //        .Where(d => d.UserId == userId && d.Status == "pending")
//    //        .ToListAsync();

//    //    foreach (var deduction in pendingDeductions)
//    //    {
//    //        deduction.Status = "applied";
//    //        deduction.UpdatedAt = DateTime.UtcNow;
//    //    }

//    //    _context.ShopSales.UpdateRange(pendingShopSales);
//    //    _context.CreditCardBankings.UpdateRange(pendingCreditCards);
//    //    _context.CashBankings.UpdateRange(pendingCash);
//    //    _context.Deductions.UpdateRange(pendingDeductions);

//    //    await _context.SaveChangesAsync();

//    //    await LogAudit(userId, "Reconciliation", 0, "CommitAllTransactions", null, new { ShopSales = pendingShopSales.Count, CreditCards = pendingCreditCards.Count, Cash = pendingCash.Count, Deductions = pendingDeductions.Count });

//    //    return true;
//    //}

//    private static ReconciliationDto MapToDto(Reconciliation reconciliation)
//    {
//        return new ReconciliationDto
//        {
//            ReconciliationId = reconciliation.ReconciliationId,
//            UserId = reconciliation.UserId,
//            ReconciliationDate = reconciliation.ReconciliationDate,
//            TotalShopSales = reconciliation.TotalShopSales,
//            TotalCreditCardTransactions = reconciliation.TotalCreditCardTransactions,
//            TotalCashTransactions = reconciliation.TotalCashTransactions,
//            TotalDeductions = reconciliation.TotalDeductions,
//            TotalLotteryAmount = reconciliation.TotalLotteryAmount,
//            NetAmount = reconciliation.NetAmount,
//            Status = reconciliation.Status,
//            Notes = reconciliation.Notes,
//            CreatedAt = reconciliation.CreatedAt,
//            UpdatedAt = reconciliation.UpdatedAt
//        };
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
