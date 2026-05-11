using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface ILlmProviderService
{
  Task<List<LlmProviderDto>> GetAllAsync(Guid userId, CancellationToken ct);
  Task<LlmProviderDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);
  Task<LlmProviderDto?> UpdateAsync(Guid userId, Guid id, UpdateLlmProviderRequest request, CancellationToken ct);
  Task<bool> EnableAsync(Guid userId, Guid id, CancellationToken ct);
  Task DisableAllAsync(Guid userId, CancellationToken ct);
  Task<LlmTestResponse?> TestAsync(Guid userId, Guid id, CancellationToken ct);
  Task<LlmProviderDto?> GetActiveAsync(Guid userId, CancellationToken ct);
  Task<RecategorizeUncategorizedResponse> RecategorizeUncategorizedAsync(Guid userId, CancellationToken ct);
}
