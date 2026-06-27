using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.Models;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/admin/active-date")]
    [Authorize]
    public class AdminActiveDateController : ControllerBase
    {
        private readonly SalesDbContext _db;

        public AdminActiveDateController(SalesDbContext db) => _db = db;

        // GET api/admin/active-date/users
        // Returns all active (non-deleted) user accounts for the admin to choose from.
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users
                .Where(u => u.IsActive && u.Role == "user")
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToListAsync();

            return Ok(users);
        }

        // GET api/admin/active-date/{userId}
        // Returns the current active-date override for a user, or null if none.
        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetOverride(int userId)
        {
            var ovr = await _db.UserActiveDateOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId);

            if (ovr is null)
                return Ok(new { hasOverride = false, activeDate = (string?)null });

            return Ok(new { hasOverride = true, activeDate = ovr.ActiveDate.ToString("yyyy-MM-dd"), setAt = ovr.SetAt });
        }

        // POST api/admin/active-date
        // Sets (or replaces) the active-date override for a user.
        [HttpPost]
        public async Task<IActionResult> SetOverride([FromBody] SetActiveDateRequest request)
        {
            var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId && u.IsActive);
            if (!userExists)
                return NotFound(new { message = "User not found." });

            var existing = await _db.UserActiveDateOverrides
                .FirstOrDefaultAsync(o => o.UserId == request.UserId);

            if (existing is null)
            {
                _db.UserActiveDateOverrides.Add(new UserActiveDateOverride
                {
                    UserId     = request.UserId,
                    ActiveDate = request.ActiveDate,
                    SetAt      = DateTime.UtcNow,
                });
            }
            else
            {
                existing.ActiveDate = request.ActiveDate;
                existing.SetAt      = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = $"Active date for user {request.UserId} set to {request.ActiveDate:dd-MM-yyyy}." });
        }

        // DELETE api/admin/active-date/{userId}
        // Removes the active-date override, returning the user to automatic date logic.
        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> ClearOverride(int userId)
        {
            var ovr = await _db.UserActiveDateOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId);

            if (ovr is null)
                return Ok(new { message = "No override was set for this user." });

            _db.UserActiveDateOverrides.Remove(ovr);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Active date override cleared." });
        }
    }

    public class SetActiveDateRequest
    {
        public int UserId { get; set; }
        public DateOnly ActiveDate { get; set; }
    }
}
