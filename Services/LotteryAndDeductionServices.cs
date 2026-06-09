using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services;

// ============ DEDUCTION SERVICE ============
//public interface IDeductionService
//{
//    Task<DeductionDto?> CreateAsync(int userId, CreateDeductionRequest request);
//    Task<DeductionDto?> GetByIdAsync(int deductionId);
//    Task<PaginatedResponse<DeductionDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);
//    Task<DeductionDto?> UpdateAsync(int deductionId, UpdateDeductionRequest request);
//    Task<bool> ApplyAsync(int deductionId);
//    Task<bool> DeleteAsync(int deductionId);
//}

//public class DeductionService : IDeductionService
//{
//    private readonly SalesDbContext _context;

//    public DeductionService(SalesDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<DeductionDto?> CreateAsync(int userId, CreateDeductionRequest request)
//    {
//        var deduction = new Deduction
//        {
//            UserId = userId,
//            TransactionNumber = GenerateTransactionNumber("DED"),
//            TransactionDate = request.TransactionDate,
//            Amount = request.Amount,
//            DeductionType = request.DeductionType,
//            Reason = request.Reason,
//            Status = "pending"
//        };

//        _context.Deductions.Add(deduction);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "Deduction", deduction.DeductionId, "Created", null, deduction);

//        return MapToDto(deduction);
//    }

//    public async Task<DeductionDto?> GetByIdAsync(int deductionId)
//    {
//        var deduction = await _context.Deductions.FirstOrDefaultAsync(d => d.DeductionId == deductionId);
//        return deduction != null ? MapToDto(deduction) : null;
//    }

//    public async Task<PaginatedResponse<DeductionDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.Deductions.Where(d => d.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var transactions = await query
//            .OrderByDescending(d => d.CreatedAt)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<DeductionDto>
//        {
//            Items = transactions.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<DeductionDto?> UpdateAsync(int deductionId, UpdateDeductionRequest request)
//    {
//        var deduction = await _context.Deductions.FirstOrDefaultAsync(d => d.DeductionId == deductionId);
//        if (deduction == null || deduction.Status != "pending")
//            return null;

//        if (!string.IsNullOrEmpty(request.DeductionType))
//            deduction.DeductionType = request.DeductionType;
//        if (!string.IsNullOrEmpty(request.Reason))
//            deduction.Reason = request.Reason;
//        if (request.Amount.HasValue && request.Amount > 0)
//            deduction.Amount = request.Amount.Value;

//        deduction.UpdatedAt = DateTime.UtcNow;
//        _context.Deductions.Update(deduction);
//        await _context.SaveChangesAsync();

//        await LogAudit(deduction.UserId, "Deduction", deductionId, "Updated", null, deduction);

//        return MapToDto(deduction);
//    }

//    public async Task<bool> ApplyAsync(int deductionId)
//    {
//        var deduction = await _context.Deductions.FirstOrDefaultAsync(d => d.DeductionId == deductionId);
//        if (deduction == null || deduction.Status != "pending")
//            return false;

//        deduction.Status = "applied";
//        deduction.UpdatedAt = DateTime.UtcNow;
//        _context.Deductions.Update(deduction);
//        await _context.SaveChangesAsync();

//        await LogAudit(deduction.UserId, "Deduction", deductionId, "Applied", null, deduction);

//        return true;
//    }

//    public async Task<bool> DeleteAsync(int deductionId)
//    {
//        var deduction = await _context.Deductions.FirstOrDefaultAsync(d => d.DeductionId == deductionId && d.Status == "pending");
//        if (deduction == null)
//            return false;

//        _context.Deductions.Remove(deduction);
//        await _context.SaveChangesAsync();

//        await LogAudit(deduction.UserId, "Deduction", deductionId, "Deleted", deduction, null);

//        return true;
//    }

//    private static DeductionDto MapToDto(Deduction deduction)
//    {
//        return new DeductionDto
//        {
//            DeductionId = deduction.DeductionId,
//            UserId = deduction.UserId,
//            TransactionNumber = deduction.TransactionNumber,
//            TransactionDate = deduction.TransactionDate,
//            Amount = deduction.Amount,
//            DeductionType = deduction.DeductionType,
//            Reason = deduction.Reason,
//            Status = deduction.Status,
//            CreatedAt = deduction.CreatedAt,
//            UpdatedAt = deduction.UpdatedAt
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

// ============ INSTANT LOTTERY SERVICE ============
//public interface IInstantLotteryService
//{
//    Task<InstantLotteryDto?> CreateAsync(int userId, CreateInstantLotteryRequest request);
//    Task<InstantLotteryDto?> GetByIdAsync(int instantLotteryId);
//    Task<PaginatedResponse<InstantLotteryDto>> GetUserTicketsAsync(int userId, int pageNumber = 1, int pageSize = 10);
//    Task<InstantLotteryDto?> UpdateAsync(int instantLotteryId, UpdateInstantLotteryRequest request);
//    Task<bool> ProcessWinAsync(int instantLotteryId, decimal prizeAmount);
//    Task<bool> RedeemAsync(int instantLotteryId);
//    Task<bool> DeleteAsync(int instantLotteryId);
//}

//public class InstantLotteryService : IInstantLotteryService
//{
//    private readonly SalesDbContext _context;

//    public InstantLotteryService(SalesDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<InstantLotteryDto?> CreateAsync(int userId, CreateInstantLotteryRequest request)
//    {
//        var lottery = new InstantLottery
//        {
//            UserId = userId,
//            TicketNumber = GenerateTicketNumber(),
//            PurchaseDate = request.PurchaseDate,
//            TicketPrice = request.TicketPrice,
//            Status = "active",
//            IsWinner = false
//        };

//        _context.InstantLotteries.Add(lottery);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "InstantLottery", lottery.InstantLotteryId, "Purchased", null, lottery);

//        return MapToDto(lottery);
//    }

//    public async Task<InstantLotteryDto?> GetByIdAsync(int instantLotteryId)
//    {
//        var lottery = await _context.InstantLotteries.FirstOrDefaultAsync(l => l.InstantLotteryId == instantLotteryId);
//        return lottery != null ? MapToDto(lottery) : null;
//    }

//    public async Task<PaginatedResponse<InstantLotteryDto>> GetUserTicketsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.InstantLotteries.Where(l => l.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var tickets = await query
//            .OrderByDescending(l => l.CreatedAt)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<InstantLotteryDto>
//        {
//            Items = tickets.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<InstantLotteryDto?> UpdateAsync(int instantLotteryId, UpdateInstantLotteryRequest request)
//    {
//        var lottery = await _context.InstantLotteries.FirstOrDefaultAsync(l => l.InstantLotteryId == instantLotteryId);
//        if (lottery == null || lottery.Status == "redeemed")
//            return null;

//        if (request.TicketPrice.HasValue && request.TicketPrice > 0)
//            lottery.TicketPrice = request.TicketPrice.Value;
//        if (request.PrizeAmount.HasValue)
//            lottery.PrizeAmount = request.PrizeAmount;
//        if (!string.IsNullOrEmpty(request.Status))
//            lottery.Status = request.Status;

//        lottery.UpdatedAt = DateTime.UtcNow;
//        _context.InstantLotteries.Update(lottery);
//        await _context.SaveChangesAsync();

//        return MapToDto(lottery);
//    }

//    //public async Task<bool> ProcessWinAsync(int instantLotteryId, decimal prizeAmount)
//    //{
//    //    var lottery = await _context.InstantLotteries.FirstOrDefaultAsync(l => l.InstantLotteryId == instantLotteryId);
//    //    if (lottery == null || lottery.Status != "active")
//    //        return false;

//    //    lottery.IsWinner = true;
//    //    lottery.PrizeAmount = prizeAmount;
//    //    lottery.Status = "won";
//    //    lottery.UpdatedAt = DateTime.UtcNow;
//    //    _context.InstantLotteries.Update(lottery);
//    //    await _context.SaveChangesAsync();

//    //    await LogAudit(lottery.UserId, "InstantLottery", instantLotteryId, "Won", null, lottery);

//    //    return true;
//    //}

//    //public async Task<bool> RedeemAsync(int instantLotteryId)
//    //{
//    //    var lottery = await _context.InstantLotteries.FirstOrDefaultAsync(l => l.InstantLotteryId == instantLotteryId);
//    //    if (lottery == null || (lottery.Status != "won" && lottery.Status != "active"))
//    //        return false;

//    //    lottery.Status = "redeemed";
//    //    lottery.UpdatedAt = DateTime.UtcNow;
//    //    _context.InstantLotteries.Update(lottery);
//    //    await _context.SaveChangesAsync();

//    //    await LogAudit(lottery.UserId, "InstantLottery", instantLotteryId, "Redeemed", null, lottery);

//    //    return true;
//    //}

//    //public async Task<bool> DeleteAsync(int instantLotteryId)
//    //{
//    //    var lottery = await _context.InstantLotteries.FirstOrDefaultAsync(l => l.InstantLotteryId == instantLotteryId && l.Status == "active");
//    //    if (lottery == null)
//    //        return false;

//    //    _context.InstantLotteries.Remove(lottery);
//    //    await _context.SaveChangesAsync();

//    //    await LogAudit(lottery.UserId, "InstantLottery", instantLotteryId, "Cancelled", lottery, null);

//    //    return true;
//    //}

//    //private static InstantLotteryDto MapToDto(InstantLottery lottery)
//    //{
//    //    return new InstantLotteryDto
//    //    {
//    //        InstantLotteryId = lottery.InstantLotteryId,
//    //        UserId = lottery.UserId,
//    //        TicketNumber = lottery.TicketNumber,
//    //        PurchaseDate = lottery.PurchaseDate,
//    //        TicketPrice = lottery.TicketPrice,
//    //        PrizeAmount = lottery.PrizeAmount,
//    //        Status = lottery.Status,
//    //        IsWinner = lottery.IsWinner,
//    //        CreatedAt = lottery.CreatedAt,
//    //        UpdatedAt = lottery.UpdatedAt
//    //    };
//    //}

//    private string GenerateTicketNumber()
//    {
//        return $"IL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 12).ToUpper()}";
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

// ============ LOTTERY SERVICE ============
//public interface ILotteryService
//{
//    Task<LotteryDto?> CreateAsync(int userId, CreateLotteryRequest request);
//    Task<LotteryDto?> GetByIdAsync(int lotteryId);
//    Task<PaginatedResponse<LotteryDto>> GetUserTicketsAsync(int userId, int pageNumber = 1, int pageSize = 10);
//    Task<LotteryDto?> UpdateAsync(int lotteryId, UpdateLotteryRequest request);
//    Task<bool> DrawAsync(int lotteryId, bool won, decimal? prizeAmount = null);
//    Task<bool> DeleteAsync(int lotteryId);
//}

//public class LotteryService : ILotteryService
//{
//    private readonly SalesDbContext _context;

//    public LotteryService(SalesDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<LotteryDto?> CreateAsync(int userId, CreateLotteryRequest request)
//    {
//        var lottery = new Lottery
//        {
//            UserId = userId,
//            LotteryName = request.LotteryName,
//            TicketNumber = GenerateTicketNumber(),
//            DrawDate = request.DrawDate,
//            TicketCost = request.TicketCost,
//            Status = "pending",
//            IsWinner = false
//        };

//        _context.Lotteries.Add(lottery);
//        await _context.SaveChangesAsync();

//        await LogAudit(userId, "Lottery", lottery.LotteryId, "Created", null, lottery);

//        return MapToDto(lottery);
//    }

//    public async Task<LotteryDto?> GetByIdAsync(int lotteryId)
//    {
//        var lottery = await _context.Lotteries.FirstOrDefaultAsync(l => l.LotteryId == lotteryId);
//        return lottery != null ? MapToDto(lottery) : null;
//    }

//    public async Task<PaginatedResponse<LotteryDto>> GetUserTicketsAsync(int userId, int pageNumber = 1, int pageSize = 10)
//    {
//        var query = _context.Lotteries.Where(l => l.UserId == userId);
//        var totalCount = await query.CountAsync();

//        var tickets = await query
//            .OrderByDescending(l => l.CreatedAt)
//            .Skip((pageNumber - 1) * pageSize)
//            .Take(pageSize)
//            .ToListAsync();

//        return new PaginatedResponse<LotteryDto>
//        {
//            Items = tickets.Select(MapToDto).ToList(),
//            TotalCount = totalCount,
//            PageNumber = pageNumber,
//            PageSize = pageSize
//        };
//    }

//    public async Task<LotteryDto?> UpdateAsync(int lotteryId, UpdateLotteryRequest request)
//    {
//        var lottery = await _context.Lotteries.FirstOrDefaultAsync(l => l.LotteryId == lotteryId);
//        if (lottery == null || lottery.Status != "pending")
//            return null;

//        if (!string.IsNullOrEmpty(request.LotteryName))
//            lottery.LotteryName = request.LotteryName;
//        if (request.TicketCost.HasValue && request.TicketCost > 0)
//            lottery.TicketCost = request.TicketCost.Value;

//        lottery.UpdatedAt = DateTime.UtcNow;
//        _context.Lotteries.Update(lottery);
//        await _context.SaveChangesAsync();

//        return MapToDto(lottery);
//    }

//    public async Task<bool> DrawAsync(int lotteryId, bool won, decimal? prizeAmount = null)
//    {
//        var lottery = await _context.Lotteries.FirstOrDefaultAsync(l => l.LotteryId == lotteryId);
//        if (lottery == null || lottery.Status != "pending")
//            return false;

//        lottery.IsWinner = won;
//        lottery.PrizeAmount = won && prizeAmount.HasValue ? prizeAmount : null;
//        lottery.Status = won ? "won" : "lost";
//        lottery.UpdatedAt = DateTime.UtcNow;
//        _context.Lotteries.Update(lottery);
//        await _context.SaveChangesAsync();

//        await LogAudit(lottery.UserId, "Lottery", lotteryId, won ? "Won" : "Lost", null, lottery);

//        return true;
//    }

//    public async Task<bool> DeleteAsync(int lotteryId)
//    {
//        var lottery = await _context.Lotteries.FirstOrDefaultAsync(l => l.LotteryId == lotteryId && l.Status == "pending");
//        if (lottery == null)
//            return false;

//        _context.Lotteries.Remove(lottery);
//        await _context.SaveChangesAsync();

//        await LogAudit(lottery.UserId, "Lottery", lotteryId, "Deleted", lottery, null);

//        return true;
//    }

//    private static LotteryDto MapToDto(Lottery lottery)
//    {
//        return new LotteryDto
//        {
//            LotteryId = lottery.LotteryId,
//            UserId = lottery.UserId,
//            LotteryName = lottery.LotteryName,
//            TicketNumber = lottery.TicketNumber,
//            DrawDate = lottery.DrawDate,
//            TicketCost = lottery.TicketCost,
//            PrizeAmount = lottery.PrizeAmount,
//            Status = lottery.Status,
//            IsWinner = lottery.IsWinner,
//            CreatedAt = lottery.CreatedAt,
//            UpdatedAt = lottery.UpdatedAt
//        };
//    }

//    private string GenerateTicketNumber()
//    {
//        return $"L-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 12).ToUpper()}";
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
