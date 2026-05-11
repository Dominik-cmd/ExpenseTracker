using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/investment-providers")]
public sealed class InvestmentProvidersController(
  IInvestmentProviderService investmentProviderService,
  ILogger<InvestmentProvidersController> logger) : ApiControllerBase
{
  [HttpGet]
  public async Task<IActionResult> GetProvidersAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentProviderService.GetProvidersAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpPatch("{id:guid}")]
  public async Task<IActionResult> UpdateProviderAsync(
    Guid id, [FromBody] UpdateInvestmentProviderRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentProviderService.UpdateProviderAsync(userId.Value, id, request, ct);
    if (!result)
    {
      return NotFound();
    }
    logger.LogInformation("User {UserId} updated investment provider {ProviderId}", userId.Value, id);
    return Ok();
  }

  [HttpPost("{id:guid}/test")]
  public async Task<IActionResult> TestProviderAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    logger.LogInformation("User {UserId} testing investment provider {ProviderId}", userId.Value, id);
    var result = await investmentProviderService.TestProviderAsync(userId.Value, id, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpPost("{id:guid}/enable")]
  public async Task<IActionResult> EnableProviderAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentProviderService.EnableProviderAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    logger.LogInformation("User {UserId} enabled investment provider {ProviderId}", userId.Value, id);
    return Ok();
  }

  [HttpPost("{id:guid}/disable")]
  public async Task<IActionResult> DisableProviderAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await investmentProviderService.DisableProviderAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    logger.LogInformation("User {UserId} disabled investment provider {ProviderId}", userId.Value, id);
    return Ok();
  }
}
