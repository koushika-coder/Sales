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

        // GET api/reports?startDate=2026-06-01&endDate=2026-06-30
        // All reconciliation reports date-wise (newest first), optionally filtered by range.
        // Accessible to both staff and admin.
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
        {
            if (startDate.HasValue && endDate.HasValue && endDate < startDate)
                return BadRequest(new { message = "Start date must be on or before end date." });

            var result = await _service.GetReportsAsync(startDate, endDate);
            return Ok(result);
        }

        // GET api/reports/download-pdf?startDate=2026-06-01&endDate=2026-06-30
        // Downloads the filtered reconciliation reports as a PDF file.
        [HttpGet("download-pdf")]
        public async Task<IActionResult> DownloadPdf([FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
        {
            if (startDate.HasValue && endDate.HasValue && endDate < startDate)
                return BadRequest(new { message = "Start date must be on or before end date." });

            var pdfBytes = await _service.GenerateReportsPdfAsync(startDate, endDate);

            var from = startDate?.ToString("yyyy-MM-dd") ?? "start";
            var to = endDate?.ToString("yyyy-MM-dd") ?? "end";
            var fileName = startDate.HasValue || endDate.HasValue
                ? $"reconciliation-reports-{from}-to-{to}.pdf"
                : "reconciliation-reports.pdf";

            return File(pdfBytes, "application/pdf", fileName);
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
