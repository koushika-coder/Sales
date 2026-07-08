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

        // Same active-date logic as SummaryService / DeductionsService.
        private async Task<(DateTime start, DateTime end)> GetActiveDateRangeAsync(int userId)
        {
            var todayUtc     = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterdayUtc = todayUtc.AddDays(-1);

            // If today is already committed, move to tomorrow.
            var todayCommitted =
                await _context.SummaryCommits.AnyAsync(c => c.Date == todayUtc) ||
                await _context.AdminReconciliations.AnyAsync(r => r.Date == todayUtc && r.Status == "submitted");

            DateOnly activeDate;
            if (todayCommitted)
            {
                activeDate = todayUtc.AddDays(1);
            }
            else
            {
                var yesterdayCommitted =
                    await _context.SummaryCommits.AnyAsync(c => c.Date == yesterdayUtc) ||
                    await _context.AdminReconciliations.AnyAsync(r => r.Date == yesterdayUtc && r.Status == "submitted");
                activeDate = yesterdayCommitted ? todayUtc : yesterdayUtc;
            }

            var start = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return (start, start.AddDays(1));
        }

        public async Task<List<LotteryInventoryResponse>> GetTodayInventory(int userId)
        {
            var (activeStart, activeEnd) = await GetActiveDateRangeAsync(userId);

            var lotteries = await _context.LotteryMaster
                .Where(x => x.IsActive)
                .ToListAsync();

            var result = new List<LotteryInventoryResponse>();

            foreach (var lottery in lotteries)
            {
                // Check if this scratch card already has saved data for the active date —
                // shared inventory count, not scoped to whoever happens to be counting it.
                var todayRecord = await _context.LotteryInventory
                    .Where(x => x.LotteryId == lottery.Id
                             && x.InventoryDate >= activeStart
                             && x.InventoryDate < activeEnd)
                    .FirstOrDefaultAsync();

                if (todayRecord != null)
                {
                    // Return the saved values so the UI shows what was entered
                    result.Add(new LotteryInventoryResponse
                    {
                        Id            = todayRecord.Id,
                        LotteryId     = lottery.Id,
                        ScratchCardNo = lottery.ScratchCardNo,
                        Price         = lottery.Price,
                        OpenNo        = todayRecord.OpenNo,
                        CloseNo       = todayRecord.CloseNo,
                        TotalSold     = todayRecord.TotalSold,
                        Sales         = todayRecord.Sales,
                    });
                }
                else
                {
                    // No record yet — derive OpenNo from the previous committed day's CloseNo
                    var lastRecord = await _context.LotteryInventory
                        .Where(x => x.LotteryId == lottery.Id
                                 && x.InventoryDate < activeStart)
                        .OrderByDescending(x => x.InventoryDate)
                        .FirstOrDefaultAsync();

                    // Admin-forced open value takes priority over last CloseNo
                    int openNo = lottery.ForcedOpenNo
                        ?? (lastRecord == null ? 1 : lastRecord.CloseNo);

                    result.Add(new LotteryInventoryResponse
                    {
                        Id            = 0,
                        LotteryId     = lottery.Id,
                        ScratchCardNo = lottery.ScratchCardNo,
                        Price         = lottery.Price,
                        OpenNo        = openNo,
                        CloseNo       = 0,
                        TotalSold     = 0,
                        Sales         = 0,
                    });
                }
            }

            return result;
        }

        public async Task<List<LotteryInventoryReportResponse>> GetInventoryReport(int userId)
        {
            var (activeStart, activeEnd) = await GetActiveDateRangeAsync(userId);
            var activeDate = DateOnly.FromDateTime(activeStart);

            // If the active date is already committed, return nothing.
            var committed =
                await _context.SummaryCommits.AnyAsync(c => c.Date == activeDate) ||
                await _context.AdminReconciliations.AnyAsync(r => r.Date == activeDate && r.Status == "submitted");

            if (committed)
                return [];

            return await (
                from inventory in _context.LotteryInventory
                join lottery in _context.LotteryMaster
                    on inventory.LotteryId equals lottery.Id
                where inventory.InventoryDate >= activeStart
                   && inventory.InventoryDate < activeEnd
                orderby lottery.ScratchCardNo
                select new LotteryInventoryReportResponse
                {
                    Id            = inventory.Id,
                    ScratchCardNo = lottery.ScratchCardNo,
                    Price         = lottery.Price,
                    OpenNo        = inventory.OpenNo,
                    CloseNo       = inventory.CloseNo,
                    TotalSold     = inventory.TotalSold,
                    Sales         = inventory.Sales,
                    InventoryDate = inventory.InventoryDate,
                    IsCommitted   = false,
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

            // Use server-side active date so it always matches what GetTodayInventory returns
            var (activeStart, activeEnd) = await GetActiveDateRangeAsync(userId);

            var totalSold = request.CloseNo - request.OpenNo;
            var sales     = totalSold * lottery.Price;

            var existingRecord = await _context.LotteryInventory
                .FirstOrDefaultAsync(x =>
                    x.LotteryId == request.LotteryId &&
                    x.InventoryDate >= activeStart &&
                    x.InventoryDate < activeEnd);

            if (existingRecord != null)
            {
                existingRecord.OpenNo  = request.OpenNo;
                existingRecord.CloseNo = request.CloseNo;
                existingRecord.TotalSold = totalSold;
                existingRecord.Sales = sales;

                existingRecord.UpdatedByUserId = userId;
                existingRecord.UpdatedDate = DateTime.UtcNow;
            }
            else
            {
                _context.LotteryInventory.Add(new LotteryInventory
                {
                    UserId        = userId,
                    LotteryId     = request.LotteryId,
                    InventoryDate = activeStart,

                    OpenNo    = request.OpenNo,
                    CloseNo   = request.CloseNo,
                    TotalSold = totalSold,
                    Sales     = sales,

                    CreatedByUserId = userId,
                    CreatedDate     = DateTime.UtcNow,
                    UpdatedByUserId = userId,
                    UpdatedDate     = DateTime.UtcNow,
                });
            }

            // Clear admin-forced open value once staff has saved for the day
            if (lottery.ForcedOpenNo.HasValue)
                lottery.ForcedOpenNo = null;

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
                        x.Id == request.Id);

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
                inventory.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        // ── Admin methods ─────────────────────────────────────────────────────

        public async Task<List<ScratchCardAdminResponse>> GetAllScratchCardsAsync()
        {
            var cards = await _context.LotteryMaster
                .Select(x => new ScratchCardAdminResponse
                {
                    Id = x.Id,
                    ScratchCardNo = x.ScratchCardNo,
                    Price = x.Price,
                    IsActive = x.IsActive,
                    ForcedOpenNo = x.ForcedOpenNo,
                    CreatedDate = x.CreatedDate,
                })
                .ToListAsync();

            return cards
                .OrderBy(x => int.TryParse(x.ScratchCardNo, out var n) ? n : int.MaxValue)
                .ThenBy(x => x.ScratchCardNo)
                .ToList();
        }

        public async Task SetOpenValueAsync(int lotteryId, int openValue)
        {
            var lottery = await _context.LotteryMaster.FindAsync(lotteryId)
                ?? throw new KeyNotFoundException("Scratch card not found.");

            lottery.ForcedOpenNo = openValue;
            await _context.SaveChangesAsync();
        }

        public async Task AddScratchCardAsync(AddScratchCardRequest request)
        {
            var exists = await _context.LotteryMaster
                .AnyAsync(x => x.ScratchCardNo == request.ScratchCardNo);

            if (exists)
                throw new InvalidOperationException("A scratch card with this number already exists.");

            _context.LotteryMaster.Add(new LotteryMaster
            {
                ScratchCardNo = request.ScratchCardNo.Trim(),
                Price = request.Price,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();
        }

        public async Task ToggleScratchCardAsync(int id)
        {
            var lottery = await _context.LotteryMaster.FindAsync(id)
                ?? throw new KeyNotFoundException("Scratch card not found.");

            lottery.IsActive = !lottery.IsActive;
            await _context.SaveChangesAsync();
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

        // Admin
        Task<List<ScratchCardAdminResponse>> GetAllScratchCardsAsync();
        Task SetOpenValueAsync(int lotteryId, int openValue);
        Task AddScratchCardAsync(AddScratchCardRequest request);
        Task ToggleScratchCardAsync(int id);
    }
}