namespace ExpenseTracker.Core.Entities;

public sealed class LlmCallLog
{
    public Guid Id { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string? ResponseRaw { get; set; }
    public string? ParsedCategory { get; set; }
    public string? ParsedSubcategory { get; set; }
    public double? ParsedConfidence { get; set; }
    public string? ParsedReasoning { get; set; }
    public long LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MerchantRaw { get; set; }
    public string? MerchantNormalized { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
