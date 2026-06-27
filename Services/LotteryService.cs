using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using Sales.Models.Sales.Models;
using static Sales.Services.LotteryService;

namespace Sales.Services
{
    public class LotteryService : ILotteryService
    {
        private readonly SalesDbContext _context;

        public LotteryService(SalesDbContext context)
        {
            _context = context;
        }

        private async Task<bool> IsDateCommittedAsync(int userId, DateOnly date) =>
            await _context.SummaryCommits.AnyAsync(c => c.UserId == userId && c.Date == date) ||
            await _context.AdminReconciliations.AnyAsync(r => r.Date == date && r.Status == "submitted");

        private async Task<(DateOnly activeDate, DateTime start, DateTime end, DateTime recordAt)> GetActiveDateRangeAsync(int userId)
        {
            // 1. Admin override takes priority.
            var ovr = await _context.UserActiveDateOverrides.FirstOrDefaultAsync(o => o.UserId == userId);
            if (ovr is not null)
            {
                if (await IsDateCommittedAsync(userId, ovr.ActiveDate))
                {
                    _context.UserActiveDateOverrides.Remove(ovr);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    var s = ovr.ActiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    var r = ovr.ActiveDate == DateOnly.FromDateTime(DateTime.UtcNow) ? DateTime.UtcNow : s.AddHours(12);
                    return (ovr.ActiveDate, s, s.AddDays(1), r);
                }
            }

            // 2. Standard yesterday / today logic.
            var todayUtc     = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterdayUtc = todayUtc.AddDays(-1);

            if (await IsDateCommittedAsync(userId, todayUtc))
            {
                var s = todayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return (todayUtc.AddDays(1), s, s.AddDays(1), DateTime.UtcNow);
            }

            var yesterdayCommitted = await IsDateCommittedAsync(userId, yesterdayUtc);
            var activeDate = yesterdayCommitted ? todayUtc : yesterdayUtc;
            var start      = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end        = start.AddDays(1);
            var recordAt   = activeDate == todayUtc ? DateTime.UtcNow : start.AddHours(12);
            return (activeDate, start, end, recordAt);
        }

        public async Task<Lottery> CreateAsync(
            int userId,
            LotteryRequest request)
        {
            var (_, start, end, recordAt) = await GetActiveDateRangeAsync(userId);

            var existing = await _context.Lotteries
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.CreatedDate >= start &&
                    x.CreatedDate < end);

            if (existing != null)
            {
                existing.LotteryValue = request.LotteryValue;
                existing.UpdatedDate  = DateTime.UtcNow;
            }
            else
            {
                existing = new Lottery
                {
                    UserId       = userId,
                    LotteryValue = request.LotteryValue,
                    CreatedDate  = recordAt,
                    UpdatedDate  = recordAt,
                };
                _context.Lotteries.Add(existing);
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Lottery?> GetByUserAsync(int userId)
        {
            var (_, start, end, _) = await GetActiveDateRangeAsync(userId);

            var record = await _context.Lotteries
                .Where(x => x.UserId == userId &&
                            x.CreatedDate >= start &&
                            x.CreatedDate < end)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();

            // Fallback: most recent record before the active date.
            return record ?? await _context.Lotteries
                .Where(x => x.UserId == userId && x.CreatedDate < start)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();
        }

        public async Task<Lottery> UpdateAsync(
            int userId,
            int id,
            LotteryRequest request)
        {
            var lottery = await _context.Lotteries
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

            if (lottery == null)
            {
                throw new Exception(
                    "Lottery record not found");
            }

            lottery.LotteryValue =
                request.LotteryValue;

            lottery.UpdatedDate =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return lottery;
        }

        public interface ILotteryService
        {
            Task<Lottery> CreateAsync(int userId, LotteryRequest request);

            Task<Lottery?> GetByUserAsync(int userId);

            Task<Lottery> UpdateAsync(
                int userId,
                int id,
                LotteryRequest request);
        }
    }
}