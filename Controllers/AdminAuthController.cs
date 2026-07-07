using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Models;
using Sales.Services;

namespace Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminAuthController(
    IAdminService adminService,
    IAuthService authService,
    IConfiguration configuration,
    IEmailService emailService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;
    private readonly IAuthService _authService = authService;
    private readonly IConfiguration _configuration = configuration;
    private readonly IEmailService _emailService = emailService;

    // Admin only — register a new admin
    [HttpPost("register")]
    public async Task<ActionResult<AdminAuthResponse>> Register(AdminRegisterRequest request)
    {
        var role = User.FindFirst("role")?.Value;
        if (role != "admin")
            return StatusCode(403, new { message = "Only admins can register other admins." });

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.PasswordHash) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Email, password, and name are required" });

        var admin = await _adminService.RegisterAsync(request, _authService);
        if (admin == null)
            return BadRequest(new { message = "Admin with this email already exists" });

        return Ok(new { message = "Admin registered successfully.", admin });
    }

    // Public — admin submits email, receives a temp password by email
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        var (success, name, tempPassword, error) = await _adminService.ForgotPasswordAsync(request.Email, _authService);
        if (!success)
            return BadRequest(new { message = error });

        // Password is already changed at this point — an email failure must not turn into an
        // unhandled 500, since the request already did what it promised on the server side.
        try { await _emailService.SendPasswordResetEmailAsync(request.Email, name ?? "Admin", tempPassword!); }
        catch { return StatusCode(502, new { message = "Password was reset, but the notification email failed to send. Contact an admin for your temporary password." }); }

        return Ok(new { message = "A temporary password has been sent to your email." });
    }

    // Admin only — reset any admin's password directly
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] AdminResetAdminPasswordRequest request)
    {
        if (HttpContext.Items["AdminId"] == null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "New password is required." });

        var (success, error) = await _adminService.AdminResetPasswordAsync(request.Email, request.NewPassword, _authService);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "Admin password reset successfully." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AdminAuthResponse>> Login(AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required" });

        var (admin, error) = await _adminService.LoginAsync(request, _authService);
        if (admin == null)
            return Unauthorized(new { message = error });

        var token = _authService.GenerateJwtToken(
            new Admin 
            { 
                AdminId = admin.AdminId, 
                Email = admin.Email, 
                Name = admin.Name, 
                Role = admin.Role 
            },
            _configuration["Jwt:Secret"] ?? string.Empty,
            _configuration["Jwt:Issuer"] ?? string.Empty,
            _configuration["Jwt:Audience"] ?? string.Empty
        );

        return Ok(new AdminAuthResponse { Token = token, Admin = admin });
    }

    [HttpGet("me")]
    public async Task<ActionResult<AdminDto>> GetCurrentAdmin()
    {
        var adminIdObj = HttpContext.Items["AdminId"];
        if (adminIdObj == null)
            return Unauthorized(new { message = "Admin not authenticated" });

        if (!int.TryParse(adminIdObj.ToString(), out var adminId))
            return Unauthorized(new { message = "Invalid admin ID" });

        var admin = await _adminService.GetAdminByIdAsync(adminId);
        if (admin == null)
            return NotFound(new { message = "Admin not found" });

        return Ok(admin);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminDto>> GetAdmin(int id)
    {
        var adminIdObj = HttpContext.Items["AdminId"];
        var adminRoleObj = HttpContext.Items["AdminRole"];

        if (adminIdObj == null)
            return Unauthorized(new { message = "Admin not authenticated" });

        // Only allow admins to view other admins
        if (adminRoleObj?.ToString() != "admin")
            return Forbid();

        var admin = await _adminService.GetAdminByIdAsync(id);
        if (admin == null)
            return NotFound(new { message = "Admin not found" });

        return Ok(admin);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AdminDto>>> GetAllAdmins([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var adminRoleObj = HttpContext.Items["AdminRole"];
        
        // Only admins can list all admins
        if (adminRoleObj?.ToString() != "admin")
            return Forbid();

        var result = await _adminService.GetAllAdminsAsync(pageNumber, pageSize);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AdminDto>> UpdateAdmin(int id, [FromBody] UpdateAdminRequest request)
    {
        var adminIdObj = HttpContext.Items["AdminId"];
        var adminRoleObj = HttpContext.Items["AdminRole"];

        if (adminIdObj == null)
            return Unauthorized(new { message = "Admin not authenticated" });

        if (adminRoleObj?.ToString() != "admin")
            return Forbid();

        var admin = await _adminService.UpdateAdminAsync(id, request.Name, request.Department);
        if (admin == null)
            return NotFound(new { message = "Admin not found" });

        return Ok(admin);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAdmin(int id)
    {
        var adminRoleObj = HttpContext.Items["AdminRole"];

        // Only admins can delete admins
        if (adminRoleObj?.ToString() != "admin")
            return Forbid();

        var result = await _adminService.DeleteAdminAsync(id);
        if (!result)
            return NotFound(new { message = "Admin not found" });

        return Ok(new { message = "Admin deleted successfully" });
    }
}

public class UpdateAdminRequest
{
    public string? Name { get; set; }
    public string? Department { get; set; }
}
