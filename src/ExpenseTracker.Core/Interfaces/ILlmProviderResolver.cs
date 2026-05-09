using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Core.Interfaces;

public interface ILlmProviderResolver
{
    Task<ILlmCategorizationProvider?> ResolveAsync(CancellationToken cancellationToken = default);
    Task<ILlmCategorizationProvider?> GetActiveProviderAsync(CancellationToken cancellationToken = default);
    Task<LlmProvider?> GetEnabledProviderAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
}
