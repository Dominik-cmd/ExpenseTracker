using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken ct);
    Task<CategoryDto?> UpdateAsync(Guid id, Guid userId, UpdateCategoryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, DeleteCategoryRequest request, CancellationToken ct);
}
