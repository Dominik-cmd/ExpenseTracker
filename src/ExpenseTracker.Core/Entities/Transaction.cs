using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid? RawMessageId { get; set; }
    public RawMessage? RawMessage { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime TransactionDate { get; set; }
    public Direction Direction { get; set; }
    public TransactionType TransactionType { get; set; }
    public string MerchantRaw { get; set; } = string.Empty;
    public string MerchantNormalized { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public CategorySource CategorySource { get; set; } = CategorySource.Default;
    public TransactionSource TransactionSource { get; set; } = TransactionSource.Sms;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
