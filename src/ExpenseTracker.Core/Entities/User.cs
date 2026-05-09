namespace ExpenseTracker.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
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
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
