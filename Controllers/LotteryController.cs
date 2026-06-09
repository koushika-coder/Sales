using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;
using static Sales.Services.LotteryService;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LotteryController : ControllerBase
    {
        private readonly ILotteryService _service;

        public LotteryController(
            ILotteryService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(
            [FromBody] LotteryRequest request)
        {
            var userIdClaim =
                User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId =
                int.Parse(userIdClaim);

            var result =
                await _service.CreateAsync(
                    userId,
                    request);

            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var userIdClaim =
                User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId =
                int.Parse(userIdClaim);

            var result =
                await _service.GetByUserAsync(
                    userId);

            return Ok(result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] LotteryRequest request)
        {
            var userIdClaim =
                User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId =
                int.Parse(userIdClaim);

            var result =
                await _service.UpdateAsync(
                    userId,
                    id,
                    request);

            return Ok(result);
        }
    }
}