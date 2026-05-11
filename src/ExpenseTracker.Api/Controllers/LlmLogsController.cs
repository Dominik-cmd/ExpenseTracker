using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/llm-logs")]
public sealed class LlmLogsController(ILlmLogService llmLogService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20,
      [FromQuery] string? provider = null,
      [FromQuery] string? purpose = null,
      [FromQuery] bool? successOnly = null,
      CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await llmLogService.GetLogsAsync(userId.Value, page, pageSize, provider, purpose, successOnly, ct);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAllAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        await llmLogService.DeleteAllAsync(userId.Value, ct);
        return NoContent();
    }
}
