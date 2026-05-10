using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/raw-messages")]
public sealed class RawMessagesController(IRawMessageService rawMessageService) : ApiControllerBase
{
  [HttpGet("queue-status")]
  public async Task<ActionResult<QueueStatusDto>> GetQueueStatusAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await rawMessageService.GetQueueStatusAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpGet]
  public async Task<ActionResult<List<RawMessageDto>>> GetAsync(
    [FromQuery] ParseStatus? status, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await rawMessageService.GetAllAsync(userId.Value, status, ct);
    return Ok(result);
  }

  [HttpPost("{id:guid}/reprocess")]
  public async Task<IActionResult> ReprocessAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await rawMessageService.ReprocessAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    return Accepted();
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await rawMessageService.DeleteAsync(userId.Value, id, ct);
    if (!result)
    {
      return NotFound();
    }
    return NoContent();
  }
}
