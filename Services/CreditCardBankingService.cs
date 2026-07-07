using Google;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using Microsoft.EntityFrameworkCore;

namespace Sales.Services
{
    public class CreditCardBankingService : ICreditCardService
    {
        private readonly SalesDbContext _context;

        public CreditCardBankingService(SalesDbContext context)
        {
            _context = context;
        }
        public async Task<CreditCardBanking> CreateAsync(
     int userId,
     CreditCardRequest request)
        {
            var today = DateTime.Today;

            var existingRecord = await _context.CreditCardBanking.FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.CreatedDate.Date == today);

            if (existingRecord != null)
            {
                existingRecord.ManualCardAmount = request.ManualCardAmount;
                existingRecord.CardAmount = request.CardAmount;

                await _context.SaveChangesAsync();

                return existingRecord;
            }

            var entity = new CreditCardBanking
            {
                UserId = userId,
                ManualCardAmount = request.ManualCardAmount,
                CardAmount = request.CardAmount,
                CreatedDate = DateTime.UtcNow
            };

            _context.CreditCardBanking.Add(entity);

            await _context.SaveChangesAsync();

            return entity;
        }
    }
    public interface ICreditCardService
    {
        Task<CreditCardBanking> CreateAsync(
            int userId,
            CreditCardRequest request);
    }
}
