using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;

namespace Sales.Services;

public interface IUserService
{
    Task<UserDto?> RegisterAsync(RegisterRequest request, IAuthService authService);
    Task<UserDto?> CreateUserByAdminAsync(CreateUserByAdminRequest request, IAuthService authService);
    Task<(UserDto? User, string? Error)> LoginAsync(LoginRequest request, IAuthService authService);
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<UserDto?> GetCurrentUserAsync(int userId);
    Task<PaginatedResponse<UserDto>> GetAllUsersAsync(int pageNumber = 1, int pageSize = 10);
    Task<UserDto?> UpdateUserAsync(int userId, string? name, string? department);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> ActivateUserAsync(int userId);
    Task<bool> HardDeleteUserAsync(int userId);
    Task<(bool Success, string? Name, string? TempPassword, string? Error)> ForgotPasswordAsync(string email, IAuthService authService);
    Task<(bool Success, string? Error)> AdminResetPasswordAsync(string email, string newPassword, IAuthService authService);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<List<(string Email, string Name)>> GetAllAdminEmailsAsync();
}

public class UserService : IUserService
{
    private readonly SalesDbContext _context;

    public UserService(SalesDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> RegisterAsync(RegisterRequest request, IAuthService authService)
    {
        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
            return null;

        var user = new User
        {
            Email = request.Email,
            PasswordHash = authService.HashPassword(request.Password),
            Name = request.Name,
            Role = request.Role,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return MapToUserDto(user);
    }

    public async Task<(UserDto? User, string? Error)> LoginAsync(LoginRequest request, IAuthService authService)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);
        
        if (user == null)
            return (null, "Invalid email or password");

        if (!authService.VerifyPassword(request.Password, user.PasswordHash))
            return (null, "Invalid email or password");

        return (MapToUserDto(user), null);
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.Users.AsQueryable();
        var totalCount = await query.CountAsync();
        
        var users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<UserDto>
        {
            Items = users.Select(MapToUserDto).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserDto?> CreateUserByAdminAsync(CreateUserByAdminRequest request, IAuthService authService)
    {
        // Check if user already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
            return null;

        var user = new User
        {
            Email = request.Email,
            PasswordHash = authService.HashPassword(request.Password),
            Name = request.Name,
            Role = "user",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return MapToUserDto(user);
    }

    public async Task<UserDto?> UpdateUserAsync(int userId, string? name, string? department)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        if (!string.IsNullOrEmpty(name))
            user.Name = name;
    
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return MapToUserDto(user);
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ActivateUserAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> HardDeleteUserAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<(bool Success, string? Name, string? TempPassword, string? Error)> ForgotPasswordAsync(string email, IAuthService authService)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user == null)
            return (false, null, null, "No active account found with that email.");

        var tempPassword = Guid.NewGuid().ToString("N")[..8];
        user.PasswordHash = authService.HashPassword(tempPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, user.Name, tempPassword, null);
    }

    public async Task<(bool Success, string? Error)> AdminResetPasswordAsync(string email, string newPassword, IAuthService authService)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return (false, "No user found with that email address.");

        if (authService.HashPassword(newPassword) == user.PasswordHash)
            return (false, "New password cannot be the same as the old password. Please create a new password.");

        user.PasswordHash = authService.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        return user != null ? MapToUserDto(user) : null;
    }

    public async Task<List<(string Email, string Name)>> GetAllAdminEmailsAsync()
    {
        var rows = await _context.Users
            .Where(u => u.Role == "admin" && u.IsActive)
            .Select(u => new { u.Email, u.Name })
            .ToListAsync();
        return rows.Select(r => (r.Email, r.Name)).ToList();
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            IsActive = user.IsActive
        };
    }
}
