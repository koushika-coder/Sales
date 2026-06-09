
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController : ControllerBase
    {
        private readonly IGmailService _gmailService;

        public GmailController(
            IGmailService gmailService)
        {
            _gmailService = gmailService;
        }

        [HttpPost("emails")]
        public async Task<IActionResult>GetEmails( GmailRequest request)
        {
            var result =
                await _gmailService
                    .GetEmailsAsync(request);

            return Ok(result);
        }
    }


}