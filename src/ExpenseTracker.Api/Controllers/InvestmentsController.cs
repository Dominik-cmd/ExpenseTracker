using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/investments")]
public sealed class InvestmentsController(IInvestmentService investmentService) : ApiControllerBase
{
  [HttpGet("summary")]
  public async Task<ActionResult<PortfolioSummaryDto>> GetSummaryAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetSummaryAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("accounts")]
  public async Task<ActionResult<List<AccountSummaryDto>>> GetAccountsAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetAccountsAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("holdings")]
  public async Task<ActionResult<List<HoldingDto>>> GetHoldingsAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetHoldingsAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("allocation")]
  public async Task<ActionResult<AllocationBreakdownDto>> GetAllocationAsync(
    [FromQuery] string type = "accountType", CancellationToken ct = default)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetAllocationAsync(userId.Value, type, ct);
    return Ok(result);
  }

  [HttpGet("history")]
  public async Task<ActionResult<List<HistoryPointDto>>> GetHistoryAsync(
    [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetHistoryAsync(userId.Value, from, to, ct);
    return Ok(result);
  }

  [HttpGet("activity")]
  public async Task<ActionResult<List<RecentActivityDto>>> GetActivityAsync(
    [FromQuery] int limit = 50, CancellationToken ct = default)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetActivityAsync(userId.Value, limit, ct);
    return Ok(result);
  }

  [HttpGet("dashboard-strip")]
  public async Task<ActionResult<DashboardStripDto>> GetDashboardStripAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetDashboardStripAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("narrative")]
  public async Task<IActionResult> GetNarrativeAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetNarrativeAsync(userId.Value, ct);
    return Ok(result ?? new { content = (string?)null });
  }

  [HttpPost("sync")]
  public async Task<IActionResult> TriggerSyncAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.TriggerSyncAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("sync/status")]
  public async Task<IActionResult> GetSyncStatusAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetSyncStatusAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("manual/accounts")]
  public async Task<ActionResult<List<ManualAccountDto>>> GetManualAccountsAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetManualAccountsAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpPost("manual/accounts")]
  public async Task<IActionResult> CreateManualAccountAsync(
    [FromBody] CreateManualAccountRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.CreateManualAccountAsync(userId.Value, request, ct);
    return Ok(result);
  }

  [HttpPatch("manual/accounts/{id:guid}")]
  public async Task<IActionResult> UpdateManualAccountAsync(
    Guid id, [FromBody] UpdateManualAccountRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.UpdateManualAccountAsync(userId.Value, id, request, ct);
    if (!result)
    {
      return NotFound();
    }
    return Ok();
  }

  [HttpDelete("manual/accounts/{id:guid}")]
  public async Task<IActionResult> DeleteManualAccountAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.DeleteManualAccountAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    return Ok();
  }

  [HttpPost("manual/accounts/{id:guid}/balance")]
  public async Task<IActionResult> UpdateBalanceAsync(
    Guid id, [FromBody] UpdateBalanceRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.UpdateBalanceAsync(userId.Value, id, request, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpGet("manual/accounts/{id:guid}/history")]
  public async Task<IActionResult> GetBalanceHistoryAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentService.GetBalanceHistoryAsync(userId.Value, id, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }
}
