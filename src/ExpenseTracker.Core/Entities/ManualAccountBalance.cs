namespace ExpenseTracker.Core.Entities;

public class ManualAccountBalance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public InvestmentAccount Account { get; set; } = null!;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
