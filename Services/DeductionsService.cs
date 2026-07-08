// Services/DeductionsService.cs
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System;

namespace Sales.Services
{
    public class DeductionsService : IDeductionsService
    {
        private readonly SalesDbContext _db;
        public DeductionsService(SalesDbContext db) => _db = db;

        private async Task<bool> IsDateCommittedAsync(int userId, DateOnly date) =>
            await _db.SummaryCommits.AnyAsync(c => c.Date == date) ||
            await _db.AdminReconciliations.AnyAsync(r => r.Date == date && r.Status == "submitted");

        private async Task<(DateOnly activeDate, DateTime start, DateTime end, DateTime recordAt)> GetActiveDateRangeAsync(int userId)
        {
            // 1. Admin override takes priority.
            var ovr = await _db.UserActiveDateOverrides.FirstOrDefaultAsync(o => o.UserId == userId);
            if (ovr is not null)
            {
                if (await IsDateCommittedAsync(userId, ovr.ActiveDate))
                {
                    _db.UserActiveDateOverrides.Remove(ovr);
                    await _db.SaveChangesAsync();
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

            var todayCommitted = await IsDateCommittedAsync(userId, todayUtc);
            if (todayCommitted)
            {
                var s = todayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return (todayUtc.AddDays(1), s, s.AddDays(1), DateTime.UtcNow);
            }

            var yesterdayCommitted = await IsDateCommittedAsync(userId, yesterdayUtc);
            var activeDate  = yesterdayCommitted ? todayUtc : yesterdayUtc;
            var start       = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end         = start.AddDays(1);
            var recordAt    = activeDate == todayUtc ? DateTime.UtcNow : start.AddHours(12);

            return (activeDate, start, end, recordAt);
        }

        public async Task<DeductionResponse?> GetTodayDeductions(int userId)
        {
            var (_, start, end, _) = await GetActiveDateRangeAsync(userId);

            var record = await _db.Deductions
                .Where(d => d.CreatedAt >= start && d.CreatedAt < end)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            // Fallback: show most recent record only if its date is not yet committed.
            if (record == null)
            {
                var prev = await _db.Deductions
                    .Where(d => d.CreatedAt < start)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();

                if (prev != null)
                {
                    var prevDate = DateOnly.FromDateTime(prev.CreatedAt);
                    if (!await IsDateCommittedAsync(userId, prevDate))
                        record = prev;
                }
            }

            if (record == null) return null;

            return new DeductionResponse
            {
                Id = record.Id,
                UserId = record.UserId,
                Cashback = record.Cashback,
                PaypointPayout = record.PaypointPayout,
                InstantLotteryPayout = record.InstantLotteryPayout,
                NewsVoucher = record.NewsVoucher,
                DDPoint = record.DDPoint,
                LotteryPayout = record.LotteryPayout,
                CreatedAt = record.CreatedAt,
            };
        }

        public async Task SaveDeductions(int userId, SaveDeductionsRequest request)
        {
            var (_, start, end, recordAt) = await GetActiveDateRangeAsync(userId);

            var existing = await _db.Deductions
                .Where(d => d.CreatedAt >= start && d.CreatedAt < end)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.Cashback = request.Cashback;
                existing.PaypointPayout = request.PaypointPayout;
                existing.InstantLotteryPayout = request.InstantLotteryPayout;
                existing.NewsVoucher = request.NewsVoucher;
                existing.DDPoint = request.DDPoint;
                existing.LotteryPayout = request.LotteryPayout;
                existing.CreatedAt = recordAt;
            }
            else
            {
                await _db.Deductions.AddAsync(new Deduction
                {
                    UserId = userId,
                    Cashback = request.Cashback,
                    PaypointPayout = request.PaypointPayout,
                    InstantLotteryPayout = request.InstantLotteryPayout,
                    NewsVoucher = request.NewsVoucher,
                    DDPoint = request.DDPoint,
                    LotteryPayout = request.LotteryPayout,
                    CreatedAt = recordAt,
                });
            }

            await _db.SaveChangesAsync();
        }
    }
    public interface IDeductionsService
    {
        Task<DeductionResponse?> GetTodayDeductions(int userId);
        Task SaveDeductions(int userId, SaveDeductionsRequest request);
    }
}