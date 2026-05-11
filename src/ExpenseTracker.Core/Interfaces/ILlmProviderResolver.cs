using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Core.Interfaces;

public interface ILlmProviderResolver
{
    Task<ILlmCategorizationProvider?> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ILlmCategorizationProvider?> GetActiveProviderAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ILlmNarrativeProvider?> GetNarrativeProviderAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LlmProvider?> GetEnabledProviderAsync(Guid userId, CancellationToken cancellationToken = default);
    void InvalidateCache();
}
