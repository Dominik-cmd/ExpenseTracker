namespace ExpenseTracker.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string? RefreshToken { get; set; }
    public string? RefreshTokenHash
    {
        get => RefreshToken;
        set => RefreshToken = value;
    }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public DateTime? RefreshTokenExpiresAt
    {
        get => RefreshTokenExpiryTime;
        set => RefreshTokenExpiryTime = value;
    }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<MerchantRule> MerchantRules { get; set; } = new List<MerchantRule>();
    public ICollection<RawMessage> RawMessages { get; set; } = new List<RawMessage>();
    public ICollection<LlmProvider> LlmProviders { get; set; } = new List<LlmProvider>();
    public ICollection<Setting> Settings { get; set; } = new List<Setting>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
