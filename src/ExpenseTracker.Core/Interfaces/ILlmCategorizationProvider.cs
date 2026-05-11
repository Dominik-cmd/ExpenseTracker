using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Records;

namespace ExpenseTracker.Core.Interfaces;

public interface ILlmCategorizationProvider
{
    LlmProviderType ProviderType { get; }

    Task<CategorizationResult?> CategorizeAsync(
        LlmProvider configuration,
        CategorizationRequest request,
        IReadOnlyCollection<Category> availableCategories,
        CancellationToken cancellationToken = default);

    Task<CategorizationResult?> CategorizeAsync(
        CategorizationRequest request,
        CancellationToken cancellationToken = default);
}
