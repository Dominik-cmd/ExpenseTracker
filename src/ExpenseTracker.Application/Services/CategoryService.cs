using System.Text.Json;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Services;

public sealed class CategoryService(
  ICategoryRepository categoryRepository,
  ITransactionRepository transactionRepository,
  IMerchantRuleRepository merchantRuleRepository,
  IAuditLogRepository auditLogRepository) : ICategoryService
{
  public async Task<List<CategoryDto>> GetAllAsync(Guid userId, CancellationToken ct)
  {
    var categories = await categoryRepository.GetRootCategoriesAsync(userId, ct);
    return categories.Select(x => x.ToDto()).ToList();
  }

  public async Task<CategoryDto> CreateAsync(
    Guid userId, CreateCategoryRequest request, CancellationToken ct)
  {
    Guid? parentCategoryId = null;

    if (request.ParentCategoryId.HasValue)
    {
      var parent = await categoryRepository.GetByIdAsync(
        request.ParentCategoryId.Value, userId, ct);

      if (parent is null)
      {
        throw new InvalidOperationException("Parent category does not exist.");
      }

      if (parent.ParentCategoryId.HasValue)
      {
        throw new InvalidOperationException("Only two category levels are supported.");
      }

      parentCategoryId = parent.Id;
    }

    var category = new Category
    {
      UserId = userId,
      Name = request.Name.Trim(),
      Color = request.Color,
      Icon = request.Icon,
      ParentCategoryId = parentCategoryId,
      SortOrder = await categoryRepository.GetNextSortOrderAsync(userId, parentCategoryId, ct),
      IsSystem = false,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };

    await categoryRepository.AddAsync(category, ct);

    await auditLogRepository.AddAsync(new AuditLog
    {
      EntityType = nameof(Category),
      EntityId = category.Id.ToString(),
      Action = "Create",
      ChangesJson = JsonSerializer.Serialize(new { request.Name, request.ParentCategoryId }),
      UserId = userId,
      CreatedAt = DateTime.UtcNow
    }, ct);

    await categoryRepository.SaveChangesAsync(ct);

    var created = await categoryRepository.GetByIdWithSubCategoriesAsync(category.Id, userId, ct);
    return created!.ToDto();
  }

  public async Task<CategoryDto?> UpdateAsync(
    Guid id, Guid userId, UpdateCategoryRequest request, CancellationToken ct)
  {
    var category = await categoryRepository.GetByIdWithSubCategoriesAsync(id, userId, ct);
    if (category is null)
    {
      return null;
    }

    if (category.IsSystem)
    {
      throw new UnauthorizedAccessException("System categories cannot be modified.");
    }

    var changes = new Dictionary<string, object?>();

    if (!string.IsNullOrWhiteSpace(request.Name))
    {
      category.Name = request.Name.Trim();
      changes[nameof(category.Name)] = category.Name;
    }

    if (request.Color is not null)
    {
      category.Color = request.Color;
      changes[nameof(category.Color)] = request.Color;
    }

    if (request.Icon is not null)
    {
      category.Icon = request.Icon;
      changes[nameof(category.Icon)] = request.Icon;
    }

    if (request.SortOrder.HasValue)
    {
      category.SortOrder = request.SortOrder.Value;
      changes[nameof(category.SortOrder)] = request.SortOrder.Value;
    }

    if (request.ExcludeFromExpenses.HasValue)
    {
      category.ExcludeFromExpenses = request.ExcludeFromExpenses.Value;
      changes[nameof(category.ExcludeFromExpenses)] = request.ExcludeFromExpenses.Value;
    }

    if (request.ExcludeFromIncome.HasValue)
    {
      category.ExcludeFromIncome = request.ExcludeFromIncome.Value;
      changes[nameof(category.ExcludeFromIncome)] = request.ExcludeFromIncome.Value;
    }

    category.UpdatedAt = DateTime.UtcNow;

    await auditLogRepository.AddAsync(new AuditLog
    {
      EntityType = nameof(Category),
      EntityId = category.Id.ToString(),
      Action = "Patch",
      ChangesJson = JsonSerializer.Serialize(changes),
      UserId = userId,
      CreatedAt = DateTime.UtcNow
    }, ct);

    await categoryRepository.SaveChangesAsync(ct);

    var updated = await categoryRepository.GetByIdWithSubCategoriesAsync(category.Id, userId, ct);
    return updated!.ToDto();
  }

  public async Task<bool> DeleteAsync(
    Guid id, Guid userId, DeleteCategoryRequest request, CancellationToken ct)
  {
    var category = await categoryRepository.GetByIdWithSubCategoriesAsync(id, userId, ct);
    if (category is null)
    {
      return false;
    }

    if (category.IsSystem)
    {
      throw new UnauthorizedAccessException("System categories cannot be deleted.");
    }

    if (request.ReassignToCategoryId == id)
    {
      throw new InvalidOperationException("Cannot reassign to the same category.");
    }

    if (!await categoryRepository.ExistsAsync(request.ReassignToCategoryId, userId, ct))
    {
      throw new InvalidOperationException("Reassignment category does not exist.");
    }

    var categoryIds = category.SubCategories.Select(x => x.Id).Append(category.Id).ToList();
    var transactions = await transactionRepository.GetByCategoryIdsAsync(categoryIds, ct);

    foreach (var transaction in transactions)
    {
      transaction.CategoryId = request.ReassignToCategoryId;
      transaction.UpdatedAt = DateTime.UtcNow;
    }

    var rules = await merchantRuleRepository.GetByCategoryIdsAsync(categoryIds, ct);

    foreach (var rule in rules)
    {
      rule.CategoryId = request.ReassignToCategoryId;
      rule.UpdatedAt = DateTime.UtcNow;
    }

    await categoryRepository.RemoveRangeAsync(category.SubCategories, ct);
    await categoryRepository.RemoveAsync(category, ct);

    await auditLogRepository.AddAsync(new AuditLog
    {
      EntityType = nameof(Category),
      EntityId = category.Id.ToString(),
      Action = "Delete",
      ChangesJson = JsonSerializer.Serialize(new { request.ReassignToCategoryId }),
      UserId = userId,
      CreatedAt = DateTime.UtcNow
    }, ct);

    await categoryRepository.SaveChangesAsync(ct);
    return true;
  }
}
