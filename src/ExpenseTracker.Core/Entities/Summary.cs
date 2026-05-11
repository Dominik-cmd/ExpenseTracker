namespace ExpenseTracker.Core.Entities;

public class Summary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SummaryType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string InputSnapshot { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string ProviderUsed { get; set; } = string.Empty;
    public int? TokensUsed { get; set; }
    public Guid UserId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
