using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/llm-logs")]
public sealed class LlmLogsController(AppDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? provider = null,
        [FromQuery] bool? successOnly = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = dbContext.LlmCallLogs.AsNoTracking().Where(x => x.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(provider))
            query = query.Where(x => x.ProviderType == provider);

        if (successOnly.HasValue)
            query = query.Where(x => x.Success == successOnly.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ProviderType,
                x.Model,
                x.MerchantRaw,
                x.MerchantNormalized,
                x.Amount,
                x.SystemPrompt,
                x.UserPrompt,
                x.ResponseRaw,
                x.ParsedCategory,
                x.ParsedSubcategory,
                x.ParsedConfidence,
                x.ParsedReasoning,
                x.LatencyMs,
                x.Success,
                x.ErrorMessage,
                x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items, totalCount = total, page, pageSize });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAllAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await dbContext.LlmCallLogs.Where(x => x.UserId == userId.Value).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}
