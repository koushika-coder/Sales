using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SummaryController : ControllerBase
    {
        private readonly ISummaryService _service;
        private readonly IAdminReconciliationService _reconciliation;

        public SummaryController(ISummaryService service, IAdminReconciliationService reconciliation)
        {
            _service = service;
            _reconciliation = reconciliation;
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("Id")?.Value;
            return claim != null && int.TryParse(claim, out userId);
        }

        // GET api/Summary/today
        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            var result = await _service.GetTodayAsync(userId);
            return Ok(result);
        }

        // PUT api/Summary
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] SummaryUpdateRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            var result = await _service.UpdateTodayAsync(userId, request);
            return Ok(result);
        }

        // GET api/Summary/zreport-comparison
        [HttpGet("zreport-comparison")]
        public async Task<IActionResult> GetZReportComparison()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            try
            {
                var result = await _service.GetZReportComparisonAsync(userId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/Summary/zreport-email
        // Returns the correct Z-report email based on commit status:
        //   • yesterday not committed  → yesterday's Z-report
        //   • yesterday committed, today not → today's Z-report
        //   • today already committed  → committed message, no email
        [HttpGet("zreport-email")]
        public async Task<IActionResult> GetZReportEmail()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            try
            {
                var result = await _service.GetZReportEmailAsync(userId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/Summary/zreport-email/by-date?date=YYYY-MM-DD
        // Checks if the given date is committed; if not, returns its Z-report email.
        [HttpGet("zreport-email/by-date")]
        public async Task<IActionResult> GetZReportEmailByDate([FromQuery] DateOnly date)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            try
            {
                var result = await _service.GetZReportEmailByDateAsync(userId, date);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/Summary/zreport-raw  — debug: returns the raw Z report email body
        [HttpGet("zreport-raw")]
        public async Task<IActionResult> GetZReportRaw([FromServices] IGmailService gmail)
        {
            var emails = await gmail.GetEmailsAsync(new GmailRequest
            {
                SubjectKeyword = "Z-Report",
                MaxResults     = 20,
            });

            var zEmail = emails.FirstOrDefault(e =>
                !e.Body.TrimStart().StartsWith('<') &&
                e.Body.Contains("GRAND TOTAL", StringComparison.OrdinalIgnoreCase));

            if (zEmail is null)
                return NotFound(new { message = "No plain-text Z-report email found." });

            return Ok(new { subject = zEmail.Subject, body = zEmail.Body });
        }

        // POST api/Summary/commit
        [HttpPost("commit")]
        public async Task<IActionResult> Commit([FromBody] SummaryCommitRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            try
            {
                var result = await _service.CommitTodayAsync(userId, request);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET api/Summary/reconciliation/portal
        // Staff portal: returns yesterday's admin-submitted reconciliation.
        // Pass ?date=YYYY-MM-DD to query a specific date.
        [HttpGet("reconciliation/portal")]
        public async Task<IActionResult> GetPortalReconciliation([FromQuery] DateOnly? date = null)
        {
            var result = await _reconciliation.GetPortalReconciliationAsync(date);

            if (result is null)
                return Ok(new { hasReconciliation = false, message = "No reconciliation available for this date." });

            return Ok(result);
        }
    }
}
