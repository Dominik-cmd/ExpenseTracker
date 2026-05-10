using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Interfaces;

public interface ILlmNarrativeProvider
{
    LlmProviderType ProviderType { get; }

    Task<NarrativeResult> GenerateAsync(
        LlmProvider configuration,
        NarrativeRequest request,
        CancellationToken cancellationToken = default);

    Task<NarrativeResult> GenerateAsync(
        NarrativeRequest request,
        CancellationToken cancellationToken = default);
}

public record NarrativeRequest(string SystemPrompt, string UserPrompt, int MaxTokens = 5000);
public record NarrativeResult(string Content, int? TokensUsed, string ModelUsed);
