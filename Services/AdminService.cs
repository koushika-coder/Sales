using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services;

public interface IAdminService
{
    Task<AdminDto?> RegisterAsync(AdminRegisterRequest request, IAuthService authService);
    Task<(AdminDto? Admin, string? Error)> LoginAsync(AdminLoginRequest request, IAuthService authService);
    Task<AdminDto?> GetAdminByIdAsync(int adminId);
    Task<PaginatedResponse<AdminDto>> GetAllAdminsAsync(int pageNumber = 1, int pageSize = 10);
    Task<AdminDto?> UpdateAdminAsync(int adminId, string? name, string? department);
    Task<bool> DeleteAdminAsync(int adminId);
    Task<bool> ActivateAdminAsync(int adminId);
    Task<bool> HardDeleteAdminAsync(int adminId);
    Task<(bool Success, string? Name, string? TempPassword, string? Error)> ForgotPasswordAsync(string email, IAuthService authService);
    Task<(bool Success, string? Error)> AdminResetPasswordAsync(string email, string newPassword, IAuthService authService);
}

public class AdminService : IAdminService
{
    private readonly SalesDbContext _context;

    public AdminService(SalesDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDto?> RegisterAsync(AdminRegisterRequest request, IAuthService authService)
    {
        // Check if admin already exists
        var existingAdmin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == request.Email);
        if (existingAdmin != null)
            return null;

        var admin = new Admin
        {
            Email = request.Email,
            PasswordHash = authService.HashPassword(request.Password),
            Name = request.Name,
            Department = request.Department,
            Role = "admin",
            IsActive = true
        };

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync();

        return MapToAdminDto(admin);
    }

    public async Task<(AdminDto? Admin, string? Error)> LoginAsync(AdminLoginRequest request, IAuthService authService)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == request.Email && a.IsActive);
        
        if (admin == null)
            return (null, "Invalid email or password");

        if (!authService.VerifyPassword(request.Password, admin.PasswordHash))
            return (null, "Invalid email or password");

        return (MapToAdminDto(admin), null);
    }

    public async Task<AdminDto?> GetAdminByIdAsync(int adminId)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId && a.IsActive);
        return admin != null ? MapToAdminDto(admin) : null;
    }

    public async Task<PaginatedResponse<AdminDto>> GetAllAdminsAsync(int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Admins.AsQueryable();
        var totalCount = await query.CountAsync();
        
        var admins = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<AdminDto>
        {
            Items = admins.Select(MapToAdminDto).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AdminDto?> UpdateAdminAsync(int adminId, string? name, string? department)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId);
        if (admin == null)
            return null;

        if (!string.IsNullOrEmpty(name))
            admin.Name = name;
        if (!string.IsNullOrEmpty(department))
            admin.Department = department;

        admin.UpdatedAt = DateTime.UtcNow;
        _context.Admins.Update(admin);
        await _context.SaveChangesAsync();

        return MapToAdminDto(admin);
    }

    public async Task<bool> DeleteAdminAsync(int adminId)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId);
        if (admin == null)
            return false;

        admin.IsActive = false;
        admin.UpdatedAt = DateTime.UtcNow;
        _context.Admins.Update(admin);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ActivateAdminAsync(int adminId)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId);
        if (admin == null)
            return false;

        admin.IsActive = true;
        admin.UpdatedAt = DateTime.UtcNow;
        _context.Admins.Update(admin);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> HardDeleteAdminAsync(int adminId)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.AdminId == adminId);
        if (admin == null)
            return false;

        _context.Admins.Remove(admin);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<(bool Success, string? Name, string? TempPassword, string? Error)> ForgotPasswordAsync(string email, IAuthService authService)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == email && a.IsActive);
        if (admin == null)
            return (false, null, null, "No active account found with that email.");

        var tempPassword = Guid.NewGuid().ToString("N")[..8];
        admin.PasswordHash = authService.HashPassword(tempPassword);
        admin.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, admin.Name, tempPassword, null);
    }

    public async Task<(bool Success, string? Error)> AdminResetPasswordAsync(string email, string newPassword, IAuthService authService)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == email);
        if (admin == null)
            return (false, "No admin found with that email address.");

        if (authService.HashPassword(newPassword) == admin.PasswordHash)
            return (false, "New password cannot be the same as the old password. Please create a new password.");

        admin.PasswordHash = authService.HashPassword(newPassword);
        admin.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    private static AdminDto MapToAdminDto(Admin admin)
    {
        return new AdminDto
        {
            AdminId = admin.AdminId,
            Email = admin.Email,
            Name = admin.Name,
            Department = admin.Department,
            Role = admin.Role,
            CreatedAt = admin.CreatedAt,
            IsActive = admin.IsActive
        };
    }
}
