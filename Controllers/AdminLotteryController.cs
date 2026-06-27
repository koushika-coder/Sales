using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/admin/lottery")]
    [Authorize]
    public class AdminLotteryController : ControllerBase
    {
        private readonly ILotteryInstanceService _service;

        public AdminLotteryController(ILotteryInstanceService service)
        {
            _service = service;
        }

        private bool IsAdmin() =>
            User.FindFirst("role")?.Value == "admin" &&
            HttpContext.Items["AdminId"] != null;

        // GET api/admin/lottery/scratch-cards
        // All scratch cards — active and inactive — for admin management
        [HttpGet("scratch-cards")]
        public async Task<IActionResult> GetAll()
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register users." });

            return Ok(await _service.GetAllScratchCardsAsync());
        }

        // POST api/admin/lottery/scratch-cards
        // Add a new scratch card — appears on staff portal immediately
        [HttpPost("scratch-cards")]
        public async Task<IActionResult> Add([FromBody] AddScratchCardRequest request)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register users." });

            if (string.IsNullOrWhiteSpace(request.ScratchCardNo))
                return BadRequest(new { message = "Scratch card number is required." });

            if (request.Price <= 0)
                return BadRequest(new { message = "Price must be greater than 0." });

            try
            {
                await _service.AddScratchCardAsync(request);
                return Ok(new { message = "Scratch card added successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT api/admin/lottery/scratch-cards/{id}/open-value
        // Reset the open number for a scratch card (1000, 0000, or custom)
        [HttpPut("scratch-cards/{id}/open-value")]
        public async Task<IActionResult> SetOpenValue(int id, [FromBody] SetOpenValueRequest request)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register users." });

            try
            {
                await _service.SetOpenValueAsync(id, request.OpenValue);
                return Ok(new { message = $"Open value set to {request.OpenValue}." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // PUT api/admin/lottery/scratch-cards/{id}/toggle
        // Activate or deactivate a scratch card
        [HttpPut("scratch-cards/{id}/toggle")]
        public async Task<IActionResult> Toggle(int id)
        {
            var role = User.FindFirst("role")?.Value;
            if (role != "admin")
                return StatusCode(403, new { message = "Only admins can register users." });

            try
            {
                await _service.ToggleScratchCardAsync(id);
                return Ok(new { message = "Scratch card status updated." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
