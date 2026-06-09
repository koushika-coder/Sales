using Microsoft.AspNetCore.Mvc;
using Sales.DTOs;
using Sales.Services;

namespace Sales.Controllers;

[ApiController]
[Route("api/reconciliation")]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;

    public ReconciliationController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryDto>> GetDailySummary([FromQuery] DateTime? date = null)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var summaryDate = date ?? DateTime.UtcNow;
        var summary = await _reconciliationService.GetDailySummaryAsync(userId, summaryDate);
        return Ok(summary);
    }

    [HttpPost]
    public async Task<ActionResult<ReconciliationDto>> CreateReconciliation(CreateReconciliationRequest request)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var reconciliation = await _reconciliationService.CreateReconciliationAsync(userId, request);
        if (reconciliation == null)
            return BadRequest(new { message = "Failed to create reconciliation" });

        return CreatedAtAction(nameof(GetReconciliationById), new { id = reconciliation.ReconciliationId }, reconciliation);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReconciliationDto>> GetReconciliationById(int id)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var reconciliation = await _reconciliationService.GetByIdAsync(id);
        if (reconciliation == null)
            return NotFound(new { message = "Reconciliation not found" });

        var currentUserId = (int)userIdObj;
        var userRoleObj = HttpContext.Items["UserRole"];
        if (currentUserId != reconciliation.UserId && userRoleObj?.ToString() != "admin")
            return Forbid();

        return Ok(reconciliation);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ReconciliationDto>>> GetMyReconciliations([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var result = await _reconciliationService.GetUserReconciliationsAsync(userId, pageNumber, pageSize);
        return Ok(result);
    }

    [HttpPost("{id}/finalize")]
    public async Task<IActionResult> FinalizeReconciliation(int id)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var reconciliation = await _reconciliationService.GetByIdAsync(id);
        if (reconciliation == null)
            return NotFound(new { message = "Reconciliation not found" });

        var currentUserId = (int)userIdObj;
        var userRoleObj = HttpContext.Items["UserRole"];
        if (currentUserId != reconciliation.UserId && userRoleObj?.ToString() != "admin")
            return Forbid();

        var success = await _reconciliationService.FinalizeReconciliationAsync(id);
        if (!success)
            return BadRequest(new { message = "Failed to finalize reconciliation (not in draft status)" });

        return Ok(new { message = "Reconciliation finalized successfully" });
    }

    [HttpPost("commit-all")]
    public async Task<IActionResult> CommitAllPendingTransactions()
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var success = await _reconciliationService.CommitAllPendingTransactionsAsync(userId);
        if (!success)
            return BadRequest(new { message = "Failed to commit transactions" });

        return Ok(new { message = "All pending transactions committed successfully" });
    }

    [HttpPost("edit/{id}")]
    public async Task<IActionResult> EditTransaction(
        [FromQuery] string transactionType,
        [FromQuery] int transactionId,
        [FromBody] object updates)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        // This endpoint is for editing transactions before they're committed
        // The specific edit logic depends on transaction type and is handled by respective services
        // For now, return a placeholder response
        return Ok(new { message = $"Edit capability for {transactionType} transaction {transactionId} is handled by specific transaction endpoints" });
    }
}

// Health Check and Stats Controller
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;

    public StatsController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpGet("today")]
    public async Task<ActionResult<SummaryDto>> GetTodaysSummary()
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var summary = await _reconciliationService.GetDailySummaryAsync(userId, DateTime.UtcNow.Date);
        return Ok(summary);
    }

    [HttpGet("date")]
    public async Task<ActionResult<SummaryDto>> GetSummaryByDate([FromQuery] DateTime date)
    {
        var userIdObj = HttpContext.Items["UserId"];
        if (userIdObj == null)
            return Unauthorized(new { message = "User not authenticated" });

        var userId = (int)userIdObj;
        var summary = await _reconciliationService.GetDailySummaryAsync(userId, date);
        return Ok(summary);
    }
}
