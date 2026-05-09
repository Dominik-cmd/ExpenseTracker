using System;

namespace ExpenseTracker.Api.Models
{


public sealed record LlmProviderDto(
    Guid Id,
    string ProviderType,
    string Name,
    string Model,
    bool IsEnabled,
    bool HasApiKey,
    DateTime? LastTestedAt,
    string? LastTestStatus);

public sealed record UpdateLlmProviderRequest(string? Model, string? ApiKey);

public sealed record LlmTestResponse(bool Success, double LatencyMs, string? ErrorMessage);

public sealed record RecategorizeUncategorizedResponse(int QueuedCount);
}

