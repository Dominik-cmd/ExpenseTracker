using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetRootCategoriesAsync(Guid userId, CancellationToken ct);
    Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Category?> GetByIdWithSubCategoriesAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Category?> GetByNameAsync(Guid userId, string name, Guid? parentId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken ct);
    Task<int> GetNextSortOrderAsync(Guid userId, Guid? parentCategoryId, CancellationToken ct);
    Task<List<Category>> GetAllForUserAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task RemoveAsync(Category category, CancellationToken ct);
    Task RemoveRangeAsync(IEnumerable<Category> categories, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
