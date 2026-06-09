using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaypointController : ControllerBase
    {
        private readonly IPaypointService _paypointService;

        public PaypointController(
            IPaypointService paypointService)
        {
            _paypointService = paypointService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(
            [FromBody] PaypointRequest request)
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized();
            }

            var userId = int.Parse(userIdClaim);

            var result = await _paypointService.CreateAsync(
                userId,
                request);

            return Ok(result);
        }
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get()
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId = int.Parse(userIdClaim);

            var result = await _paypointService.GetByUserAsync(userId);

            return Ok(result);
        }
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update( int id, [FromBody] PaypointRequest request)
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized();
            }

            var userId = int.Parse(userIdClaim);

            var result = await _paypointService.UpdateAsync(
                userId,
                id,
                request);

            return Ok(result);
        }
    }
}