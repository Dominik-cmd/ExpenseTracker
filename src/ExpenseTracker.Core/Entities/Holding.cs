namespace ExpenseTracker.Core.Entities;

public class Holding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public InvestmentAccount Account { get; set; } = null!;
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? CostBasisPerShare { get; set; }
    public decimal? MarkPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal? UnrealizedPnl { get; set; }
    public decimal? UnrealizedPnlPercent { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}
