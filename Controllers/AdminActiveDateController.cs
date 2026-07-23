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

        // GET api/admin/active-date
        // Returns the current shop-wide active-date override, or null if none.
        [HttpGet]
        public async Task<IActionResult> GetOverride()
        {
            var ovr = await _db.UserActiveDateOverrides.FirstOrDefaultAsync();

            if (ovr is null)
                return Ok(new { hasOverride = false, activeDate = (string?)null });

            return Ok(new { hasOverride = true, activeDate = ovr.ActiveDate.ToString("yyyy-MM-dd"), setAt = ovr.SetAt });
        }

        // POST api/admin/active-date
        // Sets (or replaces) the shop-wide active-date override.
        [HttpPost]
        public async Task<IActionResult> SetOverride([FromBody] SetActiveDateRequest request)
        {
            var existing = await _db.UserActiveDateOverrides.FirstOrDefaultAsync();

            if (existing is null)
            {
                _db.UserActiveDateOverrides.Add(new UserActiveDateOverride
                {
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
            return Ok(new { message = $"Active date set to {request.ActiveDate:dd-MM-yyyy} for all users." });
        }

        // DELETE api/admin/active-date
        // Removes the shop-wide active-date override, returning everyone to automatic date logic.
        [HttpDelete]
        public async Task<IActionResult> ClearOverride()
        {
            var ovr = await _db.UserActiveDateOverrides.FirstOrDefaultAsync();

            if (ovr is null)
                return Ok(new { message = "No override was set." });

            _db.UserActiveDateOverrides.Remove(ovr);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Active date override cleared." });
        }
    }

    public class SetActiveDateRequest
    {
        public DateOnly ActiveDate { get; set; }
    }
}
