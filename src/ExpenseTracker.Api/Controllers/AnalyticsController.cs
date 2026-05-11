using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(
  IAnalyticsService analyticsService,
  INarrativeService narrativeService) : ApiControllerBase
{
    [HttpGet("dashboard/strip")]
    public async Task<ActionResult<DashboardStrip>> GetDashboardStripAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetDashboardStripAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> GetDashboardAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetDashboardAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> GetMonthlyAsync(
      [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetMonthlyAsync(userId.Value, year, month, ct);
        return Ok(result);
    }

    [HttpGet("yearly")]
    public async Task<ActionResult<YearlyReportResponse>> GetYearlyAsync(
      [FromQuery] int year, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetYearlyAsync(userId.Value, year, ct);
        return Ok(result);
    }

    [HttpGet("insights")]
    public async Task<ActionResult<InsightsResponse>> GetInsightsAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetInsightsAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("cost-summary")]
    public async Task<IActionResult> GetCostSummaryAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await analyticsService.GetCostSummaryAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("dashboard/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetDashboardNarrativeAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await narrativeService.GetDashboardNarrativeAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("monthly/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetMonthlyNarrativeAsync(
      [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await narrativeService.GetMonthlyNarrativeAsync(userId.Value, year, month, ct);
        return Ok(result);
    }

    [HttpGet("yearly/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetYearlyNarrativeAsync(
      [FromQuery] int year, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await narrativeService.GetYearlyNarrativeAsync(userId.Value, year, ct);
        return Ok(result);
    }

    [HttpPost("regenerate-narratives")]
    public async Task<IActionResult> RegenerateNarrativesAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        await narrativeService.RegenerateAllAsync(userId.Value, ct);
        return Ok();
    }
}
