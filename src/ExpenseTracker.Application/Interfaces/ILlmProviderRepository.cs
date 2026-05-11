using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface ILlmProviderRepository
{
  Task<List<LlmProvider>> GetAllForUserAsync(Guid userId, CancellationToken ct);
  Task<LlmProvider?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
  Task<LlmProvider?> GetEnabledAsync(Guid userId, CancellationToken ct);
  Task DisableAllForUserAsync(Guid userId, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
