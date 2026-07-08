using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services
{
    public class PaypointService : IPaypointService
    {
        private readonly SalesDbContext _context;

        public PaypointService(SalesDbContext context)
        {
            _context = context;
        }

        private async Task<bool> IsDateCommittedAsync(int userId, DateOnly date) =>
            await _context.SummaryCommits.AnyAsync(c => c.Date == date) ||
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

        public async Task<Paypoint> CreateAsync(int userId, PaypointRequest request)
        {
            var (_, start, end, recordAt) = await GetActiveDateRangeAsync(userId);

            var existingPaypoint = await _context.Paypoints
                .FirstOrDefaultAsync(x =>
                    x.CreatedDate >= start &&
                    x.CreatedDate < end);

            if (existingPaypoint != null)
            {
                existingPaypoint.PaypointValue = request.PaypointValue;
                existingPaypoint.UpdatedDate   = DateTime.UtcNow;
            }
            else
            {
                existingPaypoint = new Paypoint
                {
                    UserId        = userId,
                    PaypointValue = request.PaypointValue,
                    CreatedDate   = recordAt,
                    UpdatedDate   = recordAt,
                };
                _context.Paypoints.Add(existingPaypoint);
            }

            await _context.SaveChangesAsync();
            return existingPaypoint;
        }

        public async Task<Paypoint?> GetByUserAsync(int userId)
        {
            var (_, start, end, _) = await GetActiveDateRangeAsync(userId);

            var record = await _context.Paypoints
                .Where(x => x.CreatedDate >= start &&
                            x.CreatedDate < end)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();

            // Fallback: most recent record before the active date, only if not yet committed.
            if (record != null) return record;

            var prev = await _context.Paypoints
                .Where(x => x.CreatedDate < start)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();

            if (prev == null) return null;
            var prevDate = DateOnly.FromDateTime(prev.CreatedDate);
            return await IsDateCommittedAsync(userId, prevDate) ? null : prev;
        }
        public async Task<Paypoint> UpdateAsync(int userId, int id,PaypointRequest request)
        {
            var paypoint = await _context.Paypoints
                .FirstOrDefaultAsync(x => x.Id == id);

            if (paypoint == null)
            {
                throw new Exception("Paypoint not found.");
            }

            paypoint.PaypointValue = request.PaypointValue;
            paypoint.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return paypoint;
        }

    }
    public interface IPaypointService
    {
        Task<Paypoint> CreateAsync(int userId, PaypointRequest request);
        Task<Paypoint> GetByUserAsync(int userId);
        Task<Paypoint> UpdateAsync(int userId, int id, PaypointRequest request);
    }
}