using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System;


namespace YourApp.Services;

public class SafeDropService : SafeDropService.ISafeDropService
{
    private readonly SalesDbContext _db;

    public SafeDropService(SalesDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────
    // Get today's record
    // ─────────────────────────────────────────
    public async Task<SafeDropResponse?> GetTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var record = await _db.SafeDrops
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Date == today);

        return record is null ? null : ToResponse(record);
    }

    // ─────────────────────────────────────────
    // Get record by specific date
    // ─────────────────────────────────────────
    public async Task<SafeDropResponse?> GetByDateAsync(DateOnly date)
    {
        var record = await _db.SafeDrops
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Date == date);

        return record is null ? null : ToResponse(record);
    }

    // ─────────────────────────────────────────
    // Create or update today's record
    // ─────────────────────────────────────────
    public async Task<SafeDropResponse> UpsertAsync(SafeDropRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var existing = await _db.SafeDrops
            .FirstOrDefaultAsync(s => s.Date == today);

        if (existing is null)
        {
            var newRecord = new SafeDrop
            {
                Date = today,
                LastSafe = request.LastSafe,
                SafeDropAmount = request.SafeDropAmount,
                CreatedAt = DateTime.UtcNow,
            };
            _db.SafeDrops.Add(newRecord);
            await _db.SaveChangesAsync();
            return ToResponse(newRecord);
        }

        existing.LastSafe = request.LastSafe;
        existing.SafeDropAmount = request.SafeDropAmount;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ToResponse(existing);
    }

    // ─────────────────────────────────────────
    // Delete today's record
    // ─────────────────────────────────────────
    public async Task<bool> DeleteTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var record = await _db.SafeDrops
            .FirstOrDefaultAsync(s => s.Date == today);

        if (record is null) return false;

        _db.SafeDrops.Remove(record);
        await _db.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────
    private static SafeDropResponse ToResponse(SafeDrop s) => new()
    {
        Id = s.Id,
        Date = s.Date,
        LastSafe = s.LastSafe,
        SafeDropAmount = s.SafeDropAmount,
        Cash = s.LastSafe + s.SafeDropAmount,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
    public interface ISafeDropService
    {
        Task<SafeDropResponse?> GetTodayAsync();
        Task<SafeDropResponse?> GetByDateAsync(DateOnly date);
        Task<SafeDropResponse> UpsertAsync(SafeDropRequest request);
        Task<bool> DeleteTodayAsync();
    }

}
