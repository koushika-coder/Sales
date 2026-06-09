// Controllers/DeductionsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeductionController : ControllerBase
    {
        private readonly IDeductionsService _service;
        public DeductionController(IDeductionsService service) => _service = service;

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("Id")?.Value;
            return claim != null && int.TryParse(claim, out userId);
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            return Ok(await _service.GetTodayDeductions(userId));
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveDeductionsRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            await _service.SaveDeductions(userId, request);
            return Ok(new { message = "Deductions saved successfully." });
        }
    }
}