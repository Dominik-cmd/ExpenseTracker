using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/merchant-rules")]
public sealed class MerchantRulesController(IMerchantRuleService merchantRuleService) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<MerchantRuleDto>>> GetAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var rules = await merchantRuleService.GetAllAsync(userId.Value, ct);
    return Ok(rules);
  }

  [HttpPatch("{id:guid}")]
  public async Task<ActionResult<MerchantRuleDto>> UpdateAsync(
    Guid id, [FromBody] UpdateMerchantRuleRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var result = await merchantRuleService.UpdateAsync(id, userId.Value, request, ct);
    if (result is null)
    {
      return NotFound();
    }
    return Ok(result);
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var deleted = await merchantRuleService.DeleteAsync(id, userId.Value, ct);
    if (!deleted)
    {
      return NotFound();
    }
    return NoContent();
  }
}
