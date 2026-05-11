using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Records;

public sealed record ProviderTestResult(
    LlmTestStatus Status,
    string? Message,
    TimeSpan? Latency);
