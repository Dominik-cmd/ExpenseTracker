namespace ExpenseTracker.Core.Entities;

public class Instrument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string AssetClass { get; set; } = string.Empty;  // STK, ETF, BOND, OPT, FUT, CASH, CRYPTO, OTHER
    public string Currency { get; set; } = string.Empty;
    public string? Sector { get; set; }
    public string? Region { get; set; }
    public string? Isin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<InvestmentTransaction> InvestmentTransactions { get; set; } = new List<InvestmentTransaction>();
}
