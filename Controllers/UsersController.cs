using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public UsersController(IUserService userService, IAuthService authService, IConfiguration configuration)
    {
        _userService = userService;
        _authService = authService;
        _configuration = configuration;
    }

    /// <summary>
    /// Admin: Create a new staff user
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> CreateStaff(CreateUserByAdminRequest request)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Email, password, and name are required" });

        var user = await _userService.CreateUserByAdminAsync(request, _authService);
        if (user == null)
            return BadRequest(new { message = "User with this email already exists" });

        return Ok(new { message = "Staff user created successfully", user });
    }

    /// <summary>
    /// User: Login
    /// </summary>
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
                Email = user.Email, 
                Name = user.Name, 
                Role = user.Role 
            },
            _configuration["Jwt:Secret"] ?? string.Empty,
            _configuration["Jwt:Issuer"] ?? string.Empty,
            _configuration["Jwt:Audience"] ?? string.Empty
        );

        return Ok(new AuthResponse { Token = token, User = user });
    }

    /// <summary>
    /// Get current user (any authenticated user)
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        if (!int.TryParse(userIdObj.ToString(), out var userId))
            return Unauthorized(new { message = "Invalid user ID" });

        var user = await _userService.GetCurrentUserAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    /// <summary>
    /// Admin: Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    /// <summary>
    /// Admin: List all staff users (paginated)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<UserDto>>> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var result = await _userService.GetAllUsersAsync(pageNumber, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Admin: Update user details (name, department)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var user = await _userService.UpdateUserAsync(id, request.Name, request.Department);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    /// <summary>
    /// Admin: Deactivate user
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var result = await _userService.DeleteUserAsync(id);
        if (!result)
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "User deactivated successfully" });
    }

    /// <summary>
    /// Admin: Activate user
    /// </summary>
    [HttpPut("{id}/activate")]
    public async Task<ActionResult> ActivateUser(int id)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var result = await _userService.ActivateUserAsync(id);
        if (!result)
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "User activated successfully" });
    }

    /// <summary>
    /// Admin: Permanently delete user
    /// </summary>
    [HttpDelete("{id}/permanent")]
    public async Task<ActionResult> HardDeleteUser(int id)
    {
        if (User.FindFirst("role")?.Value != "admin")
            return Unauthorized(new { message = "Admin not authenticated" });

        var result = await _userService.HardDeleteUserAsync(id);
        if (!result)
            return NotFound(new { message = "User not found" });

        return Ok(new { message = "User permanently deleted" });
    }
}
