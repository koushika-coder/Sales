using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/admin/reconciliation")]
    [Authorize]
    public class AdminReconciliationController(IAdminReconciliationService service) : ControllerBase
    {
        private readonly IAdminReconciliationService _service = service;

        private bool TryGetAdminId(out int adminId) =>
            int.TryParse(User.FindFirst("Id")?.Value, out adminId);

        // GET api/admin/reconciliation/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register other admins." });

            var result = await _service.GetAllPendingAsync();

            if (result.Count == 0)
                return Ok(new { hasPending = false, message = "No pending reconciliation." });

            return Ok(new { hasPending = true, items = result });
        }

        // POST api/admin/reconciliation/submit
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] AdminSubmitReconciliationRequest request)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register other admins." });
            if (!TryGetAdminId(out var adminId))
                return StatusCode(403, new { message = "Admin authentication required." });
            try
            {
                var result = await _service.SubmitAsync(adminId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/admin/reconciliation/committed
        [HttpGet("committed")]
        public async Task<IActionResult> GetAllCommitted()
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register other admins." });

            var result = await _service.GetAllCommittedAsync();
            return Ok(result);
        }

        // GET api/admin/reconciliation/committed/{date}   e.g. /committed/2026-06-12
        [HttpGet("committed/{date}")]
        public async Task<IActionResult> GetCommittedByDate(DateOnly date)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register other admins." });

            var result = await _service.GetCommittedByDateAsync(date);

            if (result is null)
                return NotFound(new { message = $"Staff have not yet committed any values for {date:dd-MM-yyyy}." });

            return Ok(result);
        }
    }
}
