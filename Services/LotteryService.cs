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

        public async Task<Lottery> CreateAsync(
            int userId,
            LotteryRequest request)
        {
            var lottery = new Lottery
            {
                UserId = userId,
                LotteryValue = request.LotteryValue,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _context.Lotteries.Add(lottery);

            await _context.SaveChangesAsync();

            return lottery;
        }

        public async Task<Lottery?> GetByUserAsync(
            int userId)
        {
            return await _context.Lotteries
                .Where(x => x.UserId == userId)
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