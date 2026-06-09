using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System;
using static YourApp.Services.SafeDropService;


namespace YourApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SafeDropController : ControllerBase
{
    private readonly ISafeDropService _service;

    public SafeDropController(ISafeDropService service)
    {
        _service = service;
    }

    // GET api/SafeDrop/today
    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var result = await _service.GetTodayAsync();
        return result is null ? NotFound("No safe drop entry found for today.") : Ok(result);
    }

    // GET api/SafeDrop/2025-06-09
    [HttpGet("{date}")]
    public async Task<IActionResult> GetByDate(DateOnly date)
    {
        var result = await _service.GetByDateAsync(date);
        return result is null ? NotFound($"No safe drop entry found for {date}.") : Ok(result);
    }

    // POST api/SafeDrop
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] SafeDropRequest request)
    {
        if (request.LastSafe < 0 || request.SafeDropAmount < 0)
            return BadRequest("Values cannot be negative.");

        var result = await _service.UpsertAsync(request);
        return Ok(result);
    }

    // DELETE api/SafeDrop/today
    [HttpDelete("today")]
    public async Task<IActionResult> DeleteToday()
    {
        var deleted = await _service.DeleteTodayAsync();
        return deleted ? NoContent() : NotFound("No safe drop entry found for today.");
    }
}
