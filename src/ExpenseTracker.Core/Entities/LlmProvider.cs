using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Entities;

public class LlmProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public LlmProviderType ProviderType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ApiKeyEncrypted { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public LlmTestStatus? LastTestStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
