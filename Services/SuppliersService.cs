// Services/SuppliersService.cs
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System;

namespace Sales.Services
{
    public class SuppliersService : ISuppliersService
    {
        private readonly SalesDbContext _db;
        public SuppliersService(SalesDbContext db) => _db = db;

        public async Task<IEnumerable<SupplierResponse>> GetAllSuppliers()
        {


            return await _db.Suppliers2
                .OrderBy(s => s.Name)
                .Select(s => new SupplierResponse
                {
                    Id = s.Id,
                    Name = s.Name,
                })
                .ToListAsync();
            
            }

        public async Task AddSupplier(AddSupplierRequest request)
        {
            try
            {


                var exists = await _db.Suppliers2
                    .AnyAsync(s => s.Name.ToLower() == request.Name.ToLower());

                if (exists)
                    throw new InvalidOperationException("A supplier with this name already exists.");

                await _db.Suppliers2.AddAsync(new Supplier
                {
                    Name = request.Name.Trim()
                });

                await _db.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task DeleteSupplier(int id)
        {
            var supplier = await _db.Suppliers2.FindAsync(id);
            if (supplier == null)
                throw new KeyNotFoundException("Supplier not found.");

            _db.Suppliers2.Remove(supplier);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<SupplierInvoiceResponse>> GetTodayInvoices(int userId)
        {
            var today = DateTime.Today;
            return await _db.SupplierInvoices
                .Include(i => i.Supplier)
                .Where(i => i.UserId == userId && i.CreatedAt.Date == today)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new SupplierInvoiceResponse
                {
                    Id = i.Id,
                    UserId = i.UserId,
                    SupplierId = i.SupplierId,
                    SupplierName = i.Supplier.Name,
                    InvoiceNo = i.InvoiceNo,
                    Value = i.Value,
                    CreatedAt = i.CreatedAt,
                })
                .ToListAsync();
        }

        public async Task AddInvoice(int userId, AddSupplierInvoiceRequest request)
        {
            var supplierExists = await _db.Suppliers2
                .AnyAsync(s => s.Id == request.SupplierId);

            if (!supplierExists)
                throw new KeyNotFoundException("Supplier not found.");

            await _db.SupplierInvoices.AddAsync(new SupplierInvoice
            {
                UserId = userId,
                SupplierId = request.SupplierId,
                InvoiceNo = request.InvoiceNo.Trim(),
                Value = request.Value,
                CreatedAt = DateTime.Now,
            });

            await _db.SaveChangesAsync();
        }

        public async Task DeleteInvoice(int userId, int invoiceId)
        {
            var invoice = await _db.SupplierInvoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.UserId == userId);

            if (invoice == null)
                throw new KeyNotFoundException("Invoice not found.");

            _db.SupplierInvoices.Remove(invoice);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<InvoiceDateSummary>> GetInvoiceDatesAsync()
        {
            return await _db.SupplierInvoices
                .GroupBy(i => DateOnly.FromDateTime(i.CreatedAt))
                .Select(g => new InvoiceDateSummary
                {
                    Date = g.Key,
                    InvoiceCount = g.Count(),
                    TotalValue = g.Sum(i => i.Value),
                })
                .OrderByDescending(x => x.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByDateAsync(DateOnly date)
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end   = start.AddDays(1);

            var invoices = await _db.SupplierInvoices
                .Include(i => i.Supplier)
                .Where(i => i.CreatedAt >= start && i.CreatedAt < end)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var userIds = invoices.Select(i => i.UserId).Distinct().ToList();
            var userNames = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            return invoices.Select(i => new SupplierInvoiceResponse
            {
                Id = i.Id,
                UserId = i.UserId,
                UserName = userNames.TryGetValue(i.UserId, out var n) ? n : $"User #{i.UserId}",
                SupplierId = i.SupplierId,
                SupplierName = i.Supplier.Name,
                InvoiceNo = i.InvoiceNo,
                Value = i.Value,
                CreatedAt = i.CreatedAt,
            });
        }
    }

    public interface ISuppliersService
    {
        Task<IEnumerable<SupplierResponse>> GetAllSuppliers();
        Task AddSupplier(AddSupplierRequest request);
        Task DeleteSupplier(int id);
        Task<IEnumerable<SupplierInvoiceResponse>> GetTodayInvoices(int userId);
        Task AddInvoice(int userId, AddSupplierInvoiceRequest request);
        Task DeleteInvoice(int userId, int invoiceId);
        Task<IEnumerable<InvoiceDateSummary>> GetInvoiceDatesAsync();
        Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByDateAsync(DateOnly date);
    }
}