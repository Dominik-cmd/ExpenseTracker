using ExpenseTracker.Core.Enums;
namespace ExpenseTracker.Core.Entities;

public class InvestmentAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProviderId { get; set; }
    public InvestmentProvider Provider { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string? ExternalAccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<InvestmentTransaction> InvestmentTransactions { get; set; } = new List<InvestmentTransaction>();
    public ManualAccountBalance? ManualBalance { get; set; }
    public ICollection<ManualBalanceHistory> BalanceHistory { get; set; } = new List<ManualBalanceHistory>();
    public ICollection<PortfolioHistory> PortfolioHistory { get; set; } = new List<PortfolioHistory>();
}
