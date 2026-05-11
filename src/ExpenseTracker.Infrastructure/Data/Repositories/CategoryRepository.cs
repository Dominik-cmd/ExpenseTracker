using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
  public async Task<List<Category>> GetRootCategoriesAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.Categories
      .Where(x => x.UserId == userId && x.ParentCategoryId == null)
      .Include(x => x.SubCategories)
      .OrderBy(x => x.SortOrder)
      .ThenBy(x => x.Name)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.Categories
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<Category?> GetByIdWithSubCategoriesAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.Categories
      .Include(x => x.SubCategories)
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<Category?> GetByNameAsync(Guid userId, string name, Guid? parentId, CancellationToken ct)
  {
    return await dbContext.Categories
      .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name && x.ParentCategoryId == parentId, ct);
  }

  public async Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.Categories.AnyAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<int> GetNextSortOrderAsync(Guid userId, Guid? parentCategoryId, CancellationToken ct)
  {
    var max = await dbContext.Categories
      .Where(x => x.UserId == userId && x.ParentCategoryId == parentCategoryId)
      .MaxAsync(x => (int?)x.SortOrder, ct);

    return (max ?? 0) + 1;
  }

  public async Task<List<Category>> GetAllForUserAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.Categories
      .Where(x => x.UserId == userId)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task AddAsync(Category category, CancellationToken ct)
  {
    await dbContext.Categories.AddAsync(category, ct);
  }

  public Task RemoveAsync(Category category, CancellationToken ct)
  {
    dbContext.Categories.Remove(category);
    return Task.CompletedTask;
  }

  public Task RemoveRangeAsync(IEnumerable<Category> categories, CancellationToken ct)
  {
    dbContext.Categories.RemoveRange(categories);
    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }
}
