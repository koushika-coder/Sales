using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/admin/reconciliation")]
    [Authorize]
    public class AdminReconciliationController(IAdminReconciliationService service, IEmailService email) : ControllerBase
    {
        private readonly IAdminReconciliationService _service = service;
        private readonly IEmailService _email = email;

        private bool TryGetAdminId(out int adminId) =>
            int.TryParse(User.FindFirst("Id")?.Value, out adminId);

        // POST api/admin/reconciliation/test-email
        // Diagnostic-only: sends a minimal test email and reports whether it actually
        // succeeded, without writing any data or touching a user's password.
        [HttpPost("test-email")]
        public async Task<IActionResult> TestEmail()
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can test email sending." });

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _email.SendTestEmailAsync();
            return Ok(new
            {
                result.Success,
                elapsedMs = sw.ElapsedMilliseconds,
                result.Host,
                result.Port,
                result.Sender,
                result.PasswordConfigured,
                result.Recipient,
                result.Error,
                result.TcpConnected,
                result.TcpElapsedMs,
                result.TcpError,
            });
        }

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

        // GET api/admin/reconciliation/committed?fromDate=2026-06-01&toDate=2026-06-30
        // Both dates are optional. When supplied, the range is inclusive.
        [HttpGet("committed")]
        public async Task<IActionResult> GetAllCommitted(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register other admins." });

            if (fromDate is not null && toDate is not null && fromDate > toDate)
                return BadRequest(new { message = "fromDate must be on or before toDate." });

            var result = await _service.GetAllCommittedAsync(fromDate, toDate);
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
