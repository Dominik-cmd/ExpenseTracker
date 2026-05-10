using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ApiControllerBase
{
  [HttpGet]
  public async Task<ActionResult<List<CategoryDto>>> GetAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var result = await categoryService.GetAllAsync(userId.Value, ct);
    return Ok(result);
  }

  [HttpPost]
  public async Task<ActionResult<CategoryDto>> CreateAsync(
    [FromBody] CreateCategoryRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var result = await categoryService.CreateAsync(userId.Value, request, ct);
    return Created($"/api/categories/{result.Id}", result);
  }

  [HttpPatch("{id:guid}")]
  public async Task<ActionResult<CategoryDto>> UpdateAsync(
    Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var result = await categoryService.UpdateAsync(id, userId.Value, request, ct);
    return result is null ? NotFound() : Ok(result);
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> DeleteAsync(
    Guid id, [FromBody] DeleteCategoryRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }

    var deleted = await categoryService.DeleteAsync(id, userId.Value, request, ct);
    return deleted ? NoContent() : NotFound();
  }
}
