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

        public SummaryController(ISummaryService service)
        {
            _service = service;
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
    }
}
