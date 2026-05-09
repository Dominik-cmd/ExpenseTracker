using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/merchant-rules")]
public sealed class MerchantRulesController(AppDbContext dbContext, ILogger<MerchantRulesController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MerchantRuleDto>>> GetAsync(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var rules = await dbContext.MerchantRules
                .AsNoTracking()
                .Include(x => x.Category)
                .ThenInclude(x => x.ParentCategory)
                .Where(x => x.UserId == userId.Value)
                .OrderBy(x => x.MerchantNormalized)
                .ToListAsync(ct);

            return Ok(rules.Select(x => x.ToDto()).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch merchant rules.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch merchant rules.");
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<MerchantRuleDto>> UpdateAsync(Guid id, [FromBody] UpdateMerchantRuleRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var rule = await dbContext.MerchantRules.Include(x => x.Category).ThenInclude(x => x.ParentCategory).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
            if (rule is null) return NotFound();
            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, ct)) return BadRequest(new { message = "Category does not exist." });

            rule.CategoryId = request.CategoryId;
            rule.UpdatedAt = DateTime.UtcNow;

            if (request.ApplyToExistingTransactions)
            {
                await dbContext.Transactions
                    .Where(x => x.MerchantNormalized == rule.MerchantNormalized && !x.IsDeleted)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.CategoryId, request.CategoryId)
                        .SetProperty(x => x.CategorySource, CategorySource.Rule)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            }

            await dbContext.SaveChangesAsync(ct);
            await dbContext.Entry(rule).Reference(x => x.Category).LoadAsync(ct);
            await dbContext.Entry(rule.Category).Reference(x => x.ParentCategory).LoadAsync(ct);
            return Ok(rule.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update merchant rule {RuleId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to update merchant rule.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var rule = await dbContext.MerchantRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
            if (rule is null) return NotFound();
            dbContext.MerchantRules.Remove(rule);
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete merchant rule {RuleId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to delete merchant rule.");
        }
    }
}
}

