using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;
using System.Security.Claims;
using static Sales.Services.LotteryInstanceService;

namespace Sales.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LotteryInstantController : ControllerBase
    {
        private readonly ILotteryInstanceService _service;

        public LotteryInstantController(
            ILotteryInstanceService service)
        {
            _service = service;
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            var result = await _service
                .GetTodayInventory(userId);

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Save(
            [FromBody] LotteryInventorySaveRequest request)
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            await _service.SaveInventory(
                userId,
                request);

            return Ok(new
            {
                success = true,
                message = "Inventory saved successfully"
            });
        }
        [HttpGet("report")]
        public async Task<IActionResult> GetReport()
        {
            var userIdClaim = User.FindFirst("Id")?.Value;
            if (userIdClaim == null)
                return Unauthorized("User ID claim not found in token.");
            if (!int.TryParse(userIdClaim, out var userId))  // ← removed .Value
                return BadRequest("Invalid user ID in token.");
            return Ok(await _service.GetInventoryReport(userId));
        }
        [HttpPut]
        public async Task<IActionResult> UpdateInventory(UpdateLotteryInventoryRequest request)
        {
            var userIdClaim = User.FindFirst("Id")?.Value;
            if (userIdClaim == null)
                return Unauthorized("User ID claim not found in token.");
            if (!int.TryParse(userIdClaim, out var userId))
                return BadRequest("Invalid user ID in token.");

            await _service.UpdateInventory(userId, request);
            return Ok(new { message = "Inventory updated successfully" });
        }
    }
}