// Controllers/SuppliersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SuppliersController : ControllerBase
    {
        private readonly ISuppliersService _service;
        public SuppliersController(ISuppliersService service) => _service = service;

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst("Id")?.Value;
            return claim != null && int.TryParse(claim, out userId);
        }

        // ── Admin endpoints ──

        [HttpGet]
        public async Task<IActionResult> GetAll() =>
            Ok(await _service.GetAllSuppliers());

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddSupplier([FromBody] AddSupplierRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Supplier name is required.");

            await _service.AddSupplier(request);
            return Ok(new { message = "Supplier added successfully." });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteSupplier(int id)
        {
            await _service.DeleteSupplier(id);
            return Ok(new { message = "Supplier deleted." });
        }

        // ── User endpoints ──

        [HttpGet("invoices/today")]
        public async Task<IActionResult> GetTodayInvoices()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            return Ok(await _service.GetTodayInvoices(userId));
        }

        [HttpPost("invoices")]
        public async Task<IActionResult> AddInvoice([FromBody] AddSupplierInvoiceRequest request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");
            if (request.SupplierId <= 0)
                return BadRequest("Select a valid supplier.");
            if (string.IsNullOrWhiteSpace(request.InvoiceNo))
                return BadRequest("Invoice No is required.");
            if (request.Value <= 0)
                return BadRequest("Value must be greater than 0.");

            await _service.AddInvoice(userId, request);
            return Ok(new { message = "Invoice added successfully." });
        }

        [HttpDelete("invoices/{id}")]
        public async Task<IActionResult> DeleteInvoice(int id)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("User ID claim not found.");

            await _service.DeleteInvoice(userId, id);
            return Ok(new { message = "Invoice deleted." });
        }
    }
}