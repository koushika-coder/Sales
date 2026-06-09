using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Models;
using Sales.Services;
using System.Security.Claims;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CreditCardBankingController : ControllerBase
    {
        private readonly ICreditCardService _creditCardBankingService;

        public CreditCardBankingController(
            ICreditCardService creditCardBankingService)
        {
            _creditCardBankingService = creditCardBankingService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreditCardRequest request)
        {
            var userIdClaim = User.FindFirst("Id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized();
            }

            var userId = int.Parse(userIdClaim);

            var result = await _creditCardBankingService.CreateAsync( userId,request);

            return Ok(result);
        }
    }
}