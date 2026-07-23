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

        private async Task<bool> IsDateCommittedAsync(DateOnly date) =>
            await _db.SummaryCommits.AnyAsync(c => c.Date == date) ||
            await _db.AdminReconciliations.AnyAsync(r => r.Date == date && r.Status == "submitted");

        // "Active date" mirrors SummaryService.GetActiveDateAsync — yesterday while it's
        // still uncommitted, otherwise today — so invoices saved while working on an
        // uncommitted day actually show up under that day instead of literal calendar
        // "today" (DateTime.Today was also server-local, not UTC, compounding the bug).
        // Also honours the shop-wide admin active-date override, same as every other
        // module — this was missing before, which let invoices land on a different
        // date than the rest of the day's data whenever an override was active.
        private async Task<DateOnly> GetActiveDateAsync()
        {
            var ovr = await _db.UserActiveDateOverrides.FirstOrDefaultAsync();
            if (ovr is not null)
            {
                if (await IsDateCommittedAsync(ovr.ActiveDate))
                {
                    _db.UserActiveDateOverrides.Remove(ovr);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    return ovr.ActiveDate;
                }
            }

            var today     = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            if (await IsDateCommittedAsync(today)) return today.AddDays(1);

            var yesterdayCommitted = await IsDateCommittedAsync(yesterday);
            return yesterdayCommitted ? today : yesterday;
        }

        public async Task<IEnumerable<SupplierInvoiceResponse>> GetTodayInvoices(int userId)
        {
            var activeDate = await GetActiveDateAsync();
            var start = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end   = start.AddDays(1);

            return await _db.SupplierInvoices
                .Include(i => i.Supplier)
                .Where(i => i.CreatedAt >= start && i.CreatedAt < end)
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

            // Mirror SummaryService's recordAt logic: while yesterday is still uncommitted,
            // "active date" is yesterday, and GetTodayInvoices queries yesterday's date
            // window. Stamping with raw UtcNow would put the row in today's window instead,
            // so it would never show up under /invoices/today until the day rolls over again.
            var activeDate  = await GetActiveDateAsync();
            var activeStart = activeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var recordAt    = activeDate == DateOnly.FromDateTime(DateTime.UtcNow)
                                ? DateTime.UtcNow
                                : activeStart.AddHours(12);

            await _db.SupplierInvoices.AddAsync(new SupplierInvoice
            {
                UserId = userId,
                SupplierId = request.SupplierId,
                InvoiceNo = request.InvoiceNo.Trim(),
                Value = request.Value,
                CreatedAt = recordAt,
            });

            await _db.SaveChangesAsync();
        }

        public async Task DeleteInvoice(int userId, int invoiceId)
        {
            var invoice = await _db.SupplierInvoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                throw new KeyNotFoundException("Invoice not found.");

            _db.SupplierInvoices.Remove(invoice);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<InvoiceDateSummary>> GetInvoiceDatesAsync(DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var query = _db.SupplierInvoices.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(i => DateOnly.FromDateTime(i.CreatedAt) >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(i => DateOnly.FromDateTime(i.CreatedAt) <= endDate.Value);

            return await query
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

            return await GetInvoicesByRangeAsyncInternal(start, end);
        }

        public async Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByRangeAsync(DateOnly? startDate, DateOnly? endDate)
        {
            var start = startDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = endDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            if (start.HasValue && end.HasValue)
                return await GetInvoicesByRangeAsyncInternal(start.Value, end.Value);

            if (start.HasValue)
                return await GetInvoicesByRangeAsyncInternal(start.Value, DateTime.MaxValue);

            if (end.HasValue)
                return await GetInvoicesByRangeAsyncInternal(DateTime.MinValue, end.Value);

            return await GetInvoicesByRangeAsyncInternal(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        }

        private async Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByRangeAsyncInternal(DateTime start, DateTime end)
        {
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
        Task<IEnumerable<InvoiceDateSummary>> GetInvoiceDatesAsync(DateOnly? startDate = null, DateOnly? endDate = null);
        Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByDateAsync(DateOnly date);
        Task<IEnumerable<SupplierInvoiceResponse>> GetInvoicesByRangeAsync(DateOnly? startDate, DateOnly? endDate);
    }
}