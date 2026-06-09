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

        public async Task<DeductionResponse?> GetTodayDeductions(int userId)
        {
            var today = DateTime.Today;
            var record = await _db.Deductions
                .Where(d => d.UserId == userId && d.CreatedAt.Date == today)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

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
                CreatedAt = record.CreatedAt,
            };
        }

        public async Task SaveDeductions(int userId, SaveDeductionsRequest request)
        {
            var today = DateTime.Today;
            var existing = await _db.Deductions
                .Where(d => d.UserId == userId && d.CreatedAt.Date == today)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.Cashback = request.Cashback;
                existing.PaypointPayout = request.PaypointPayout;
                existing.InstantLotteryPayout = request.InstantLotteryPayout;
                existing.NewsVoucher = request.NewsVoucher;
                existing.DDPoint = request.DDPoint;
                existing.CreatedAt = DateTime.Now;
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
                    CreatedAt = DateTime.Now,
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