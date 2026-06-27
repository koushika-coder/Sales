using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _service;

        public ReportsController(IReportService service)
        {
            _service = service;
        }

        private bool IsAdmin() =>
            User.FindFirst("role")?.Value == "admin" &&
            HttpContext.Items["AdminId"] != null;

        // GET api/reports
        // All reconciliation reports date-wise (newest first).
        // Accessible to both staff and admin.
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllReportsAsync();
            return Ok(result);
        }

        // GET api/reports/2026-06-12
        // Full breakdown: staff entered vs Z-Report values per field, variance, and who did the reconciliation.
        [HttpGet("{date}")]
        public async Task<IActionResult> GetByDate(DateOnly date)
        {
            var result = await _service.GetReportByDateAsync(date);

            if (result is null)
                return NotFound(new { message = $"No committed reconciliation found for {date:yyyy-MM-dd}." });

            return Ok(result);
        }
    }
}
