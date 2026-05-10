namespace ExpenseTracker.Core.Entities;

public class PortfolioHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public InvestmentAccount Account { get; set; } = null!;
    public DateOnly SnapshotDate { get; set; }
    public decimal MarketValue { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Source { get; set; } = string.Empty;  // "sync" (IBKR) | "manual"
}
