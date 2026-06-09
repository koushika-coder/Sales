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

        public async Task<Paypoint> CreateAsync(int userId, PaypointRequest request)
        {
            var today = DateTime.UtcNow.Date;

            var existingPaypoint = await _context.Paypoints
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.CreatedDate.Date == today);

            if (existingPaypoint != null)
            {
                // Update existing record for today
                existingPaypoint.PaypointValue = request.PaypointValue;
                existingPaypoint.UpdatedDate = DateTime.UtcNow;
            }
            else
            {
                // Create new record
                existingPaypoint = new Paypoint
                {
                    UserId = userId,
                    PaypointValue = request.PaypointValue,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };

                _context.Paypoints.Add(existingPaypoint);
            }

            await _context.SaveChangesAsync();

            return existingPaypoint;
        }
        public async Task<Paypoint?> GetByUserAsync(int userId)
        {
            return await _context.Paypoints
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();
        }
        public async Task<Paypoint> UpdateAsync(int userId, int id,PaypointRequest request)
        {
            var paypoint = await _context.Paypoints
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.UserId == userId);

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