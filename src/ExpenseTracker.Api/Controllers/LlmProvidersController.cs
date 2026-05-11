using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/llm-providers")]
public sealed class LlmProvidersController(ILlmProviderService llmProviderService) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<LlmProviderDto>>> GetAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.GetAllAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<LlmProviderDto>> GetByIdAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.GetByIdAsync(userId.Value, id, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpPatch("{id:guid}")]
  public async Task<ActionResult<LlmProviderDto>> UpdateAsync(
    Guid id, [FromBody] UpdateLlmProviderRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.UpdateAsync(userId.Value, id, request, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpPost("{id:guid}/enable")]
  public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.EnableAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    return NoContent();
  }

  [HttpPost("disable-all")]
  public async Task<IActionResult> DisableAllAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    await llmProviderService.DisableAllAsync(userId.Value, ct);
    return NoContent();
  }

  [HttpPost("{id:guid}/test")]
  public async Task<ActionResult<LlmTestResponse>> TestAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.TestAsync(userId.Value, id, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpGet("active")]
  public async Task<ActionResult<LlmProviderDto?>> GetActiveAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.GetActiveAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpPost("recategorize-uncategorized")]
  public async Task<ActionResult<RecategorizeUncategorizedResponse>> RecategorizeUncategorizedAsync(
    CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await llmProviderService.RecategorizeUncategorizedAsync(userId.Value, ct);
    return Ok(result);
  }
}
