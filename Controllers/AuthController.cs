using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;

    public AuthController(IUserService userService, IAuthService authService, IConfiguration configuration, IEmailService emailService)
    {
        _userService  = userService;
        _authService  = authService;
        _configuration = configuration;
        _emailService  = emailService;
    }

    // Admin only — register a new user
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var role = User.FindFirst("role")?.Value;
        if (role != "admin")
            return StatusCode(403, new { message = "Only admins can register users." });

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Email, password, and name are required" });

        var user = await _userService.RegisterAsync(request, _authService);
        if (user == null)
            return BadRequest(new { message = "User with this email already exists" });

        return Ok(new { message = "User registered successfully.", user });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required" });

        var (user, error) = await _userService.LoginAsync(request, _authService);
        if (user == null)
            return Unauthorized(new { message = error });

        var token = _authService.GenerateJwtToken(
            new Sales.Models.User
            {
                Id    = user.UserId,
                Email = user.Email,
                Name  = user.Name,
                Role  = user.Role
            },
            _configuration["Jwt:Secret"]   ?? string.Empty,
            _configuration["Jwt:Issuer"]   ?? string.Empty,
            _configuration["Jwt:Audience"] ?? string.Empty
        );

        return Ok(new AuthResponse { Token = token, User = user });
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var user = await _userService.GetCurrentUserAsync((int)userIdObj);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    // Public — user submits email, receives a temp password by email
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        var (success, name, tempPassword, error) = await _userService.ForgotPasswordAsync(request.Email, _authService);
        if (!success)
            return BadRequest(new { message = error });

        await _emailService.SendPasswordResetEmailAsync(request.Email, name ?? "User", tempPassword!);

        return Ok(new { message = "A temporary password has been sent to your email." });
    }

    // Admin only — reset any user's password directly
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] AdminResetUserPasswordRequest request)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return StatusCode(403, new { message = "Only admins can reset user passwords." });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "New password is required." });

        var (success, error) = await _userService.AdminResetPasswordAsync(request.Email, request.NewPassword, _authService);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "User password reset successfully." });
    }
}
