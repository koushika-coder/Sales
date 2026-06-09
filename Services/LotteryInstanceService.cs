using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services
{
    public class LotteryInstanceService : ILotteryInstanceService
    {
        private readonly SalesDbContext _context;

        public LotteryInstanceService(SalesDbContext context)
        {
            _context = context;
        }

        public async Task<List<LotteryInventoryResponse>> GetTodayInventory(int userId)
        {
            var lotteries = await _context.LotteryMaster
                .Where(x => x.IsActive)
                .ToListAsync();

            var result = new List<LotteryInventoryResponse>();

            foreach (var lottery in lotteries)
            {
                var lastRecord = await _context.LotteryInventory
                    .Where(x =>
                        x.UserId == userId &&
                        x.LotteryId == lottery.Id)
                    .OrderByDescending(x => x.InventoryDate)
                    .FirstOrDefaultAsync();

                result.Add(new LotteryInventoryResponse
                {
                    LotteryId = lottery.Id,
                    ScratchCardNo = lottery.ScratchCardNo,
                    Price = lottery.Price,

                    OpenNo = lastRecord == null
                        ? 1
                        : lastRecord.CloseNo,

                    CloseNo = 0,
                    TotalSold = 0,
                    Sales = 0
                });
            }

            return result;
        }

        public async Task<List<LotteryInventoryReportResponse>> GetInventoryReport(int userId)
        {
            return await (
                from inventory in _context.LotteryInventory
                join lottery in _context.LotteryMaster
                    on inventory.LotteryId equals lottery.Id
                where inventory.UserId == userId
                orderby inventory.InventoryDate descending
                select new LotteryInventoryReportResponse
                {
                    Id = inventory.Id,
                    ScratchCardNo = lottery.ScratchCardNo,
                    Price = lottery.Price,

                    OpenNo = inventory.OpenNo,
                    CloseNo = inventory.CloseNo,

                    TotalSold = inventory.TotalSold,
                    Sales = inventory.Sales,

                    InventoryDate = inventory.InventoryDate
                }
            ).ToListAsync();
        }

        public async Task SaveInventory(
            int userId,
            LotteryInventorySaveRequest request)
        {
            var lottery = await _context.LotteryMaster
                .FirstOrDefaultAsync(x =>
                    x.Id == request.LotteryId &&
                    x.IsActive);

            if (lottery == null)
                throw new Exception("Lottery not found.");

            var totalSold =
                request.CloseNo - request.OpenNo;

            var sales =
                totalSold * lottery.Price;

            var existingRecord = await _context.LotteryInventory
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.LotteryId == request.LotteryId &&
                    x.InventoryDate.Date ==
                    request.InventoryDate.Date);

            if (existingRecord != null)
            {
                existingRecord.OpenNo = request.OpenNo;
                existingRecord.CloseNo = request.CloseNo;
                existingRecord.TotalSold = totalSold;
                existingRecord.Sales = sales;

                existingRecord.UpdatedByUserId = userId;
                existingRecord.UpdatedDate = DateTime.Now;
            }
            else
            {
                var inventory = new LotteryInventory
                {
                    UserId = userId,

                    LotteryId = request.LotteryId,
                    InventoryDate = request.InventoryDate.Date,

                    OpenNo = request.OpenNo,
                    CloseNo = request.CloseNo,

                    TotalSold = totalSold,
                    Sales = sales,

                    CreatedByUserId = userId,
                    CreatedDate = DateTime.Now,

                    UpdatedByUserId = userId,
                    UpdatedDate = DateTime.Now
                };

                _context.LotteryInventory.Add(inventory);
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateInventory(
            int userId,
            UpdateLotteryInventoryRequest request)
        {
            try
            {
                var inventory = await _context.LotteryInventory
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.Id &&
                        x.UserId == userId);

                if (inventory == null)
                    throw new Exception("Inventory record not found.");

                var lottery = await _context.LotteryMaster
                    .FirstOrDefaultAsync(x =>
                        x.Id == inventory.LotteryId);

                if (lottery == null)
                    throw new Exception("Lottery not found.");

                // Update price in LotteryMaster
                lottery.Price = request.Price;

                // Update inventory values
                inventory.OpenNo = request.OpenNo;
                inventory.CloseNo = request.CloseNo;

                inventory.TotalSold =
                    request.CloseNo - request.OpenNo;

                inventory.Sales =
                    inventory.TotalSold * request.Price;

                inventory.UpdatedByUserId = userId;
                inventory.UpdatedDate = DateTime.Now;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        
        }
    }

    public interface ILotteryInstanceService
    {
        Task<List<LotteryInventoryResponse>>
            GetTodayInventory(int userId);

        Task<List<LotteryInventoryReportResponse>>
            GetInventoryReport(int userId);

        Task SaveInventory(
            int userId,
            LotteryInventorySaveRequest request);

        Task UpdateInventory(
            int userId,
            UpdateLotteryInventoryRequest request);
    }
}