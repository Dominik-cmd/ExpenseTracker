namespace ExpenseTracker.Core.Entities;

public class InvestmentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public InvestmentAccount Account { get; set; } = null!;
    public Guid? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;  // BUY, SELL, DIVIDEND, DEPOSIT, WITHDRAWAL, FEE, INTEREST, FX, OTHER
    public DateTime TransactionDate { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal? GrossAmount { get; set; }
    public decimal Commission { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
