using ExpenseTracker.Core.Enums;
namespace ExpenseTracker.Core.Entities;

public class InvestmentProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public InvestmentProviderType ProviderType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? ApiTokenEncrypted { get; set; }
    public string? ExtraConfig { get; set; }  // JSON
    public bool IsEnabled { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }  // "success" | "failure" | "never" | "n/a"
    public string? LastSyncError { get; set; }
    public DateTime? LastTestAt { get; set; }
    public string? LastTestStatus { get; set; }  // "success" | "failure" | "untested"
    public string? LastTestError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<InvestmentAccount> Accounts { get; set; } = new List<InvestmentAccount>();
}
