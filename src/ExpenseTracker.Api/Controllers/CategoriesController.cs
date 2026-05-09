using System.Text.Json;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(AppDbContext dbContext, ILogger<CategoriesController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAsync(CancellationToken ct)
    {
        try
        {
            var categories = await dbContext.Categories
                .AsNoTracking()
                .Where(x => x.ParentCategoryId == null)
                .Include(x => x.SubCategories)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(ct);

            return Ok(categories.Select(x => x.ToDto()).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch categories.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch categories.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateAsync([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        try
        {
            Guid? parentCategoryId = null;
            if (request.ParentCategoryId.HasValue)
            {
                var parent = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == request.ParentCategoryId.Value, ct);
                if (parent is null) return BadRequest(new { message = "Parent category does not exist." });
                if (parent.ParentCategoryId.HasValue) return BadRequest(new { message = "Only two category levels are supported." });
                parentCategoryId = parent.Id;
            }

            var category = new Category
            {
                Name = request.Name.Trim(),
                Color = request.Color,
                Icon = request.Icon,
                ParentCategoryId = parentCategoryId,
                SortOrder = await GetNextSortOrderAsync(parentCategoryId, ct),
                IsSystem = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Categories.Add(category);
            AddAuditLog(category.Id, "Create", new { request.Name, request.ParentCategoryId });
            await dbContext.SaveChangesAsync(ct);
            var created = await dbContext.Categories.AsNoTracking().Include(x => x.SubCategories).FirstAsync(x => x.Id == category.Id, ct);
            return Created($"/api/categories/{category.Id}", created.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create category.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to create category.");
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> UpdateAsync(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var category = await dbContext.Categories.Include(x => x.SubCategories).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (category is null) return NotFound();
            if (category.IsSystem) return Forbid();

            var changes = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(request.Name)) { category.Name = request.Name.Trim(); changes[nameof(category.Name)] = category.Name; }
            if (request.Color is not null) { category.Color = request.Color; changes[nameof(category.Color)] = request.Color; }
            if (request.Icon is not null) { category.Icon = request.Icon; changes[nameof(category.Icon)] = request.Icon; }
            if (request.SortOrder.HasValue) { category.SortOrder = request.SortOrder.Value; changes[nameof(category.SortOrder)] = request.SortOrder.Value; }
            if (request.ExcludeFromExpenses.HasValue) { category.ExcludeFromExpenses = request.ExcludeFromExpenses.Value; changes[nameof(category.ExcludeFromExpenses)] = request.ExcludeFromExpenses.Value; }
            category.UpdatedAt = DateTime.UtcNow;

            AddAuditLog(category.Id, "Patch", changes);
            await dbContext.SaveChangesAsync(ct);
            var updated = await dbContext.Categories.AsNoTracking().Include(x => x.SubCategories).FirstAsync(x => x.Id == category.Id, ct);
            return Ok(updated.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update category {CategoryId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to update category.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromBody] DeleteCategoryRequest request, CancellationToken ct)
    {
        try
        {
            var category = await dbContext.Categories.Include(x => x.SubCategories).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (category is null) return NotFound();
            if (category.IsSystem) return Forbid();
            if (request.ReassignToCategoryId == id) return BadRequest(new { message = "Cannot reassign to the same category." });
            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.ReassignToCategoryId, ct)) return BadRequest(new { message = "Reassignment category does not exist." });

            var categoryIds = category.SubCategories.Select(x => x.Id).Append(category.Id).ToList();
            var transactions = await dbContext.Transactions.Where(x => categoryIds.Contains(x.CategoryId)).ToListAsync(ct);
            foreach (var transaction in transactions)
            {
                transaction.CategoryId = request.ReassignToCategoryId;
                transaction.UpdatedAt = DateTime.UtcNow;
            }

            var rules = await dbContext.MerchantRules.Where(x => categoryIds.Contains(x.CategoryId)).ToListAsync(ct);
            foreach (var rule in rules)
            {
                rule.CategoryId = request.ReassignToCategoryId;
                rule.UpdatedAt = DateTime.UtcNow;
            }

            dbContext.Categories.RemoveRange(category.SubCategories);
            dbContext.Categories.Remove(category);
            AddAuditLog(category.Id, "Delete", new { request.ReassignToCategoryId });
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete category {CategoryId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to delete category.");
        }
    }

    private async Task<int> GetNextSortOrderAsync(Guid? parentCategoryId, CancellationToken ct)
        => (await dbContext.Categories.Where(x => x.ParentCategoryId == parentCategoryId).Select(x => (int?)x.SortOrder).MaxAsync(ct) ?? 0) + 1;

    private void AddAuditLog(Guid categoryId, string action, object changes)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityType = nameof(Category),
            EntityId = categoryId.ToString(),
            Action = action,
            ChangesJson = JsonSerializer.Serialize(changes),
            UserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
}

