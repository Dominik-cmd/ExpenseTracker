using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Entities;

public class RawMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime SentAt
    {
        get => ReceivedAt;
        set => ReceivedAt = value;
    }
    public ParseStatus ParseStatus { get; set; } = ParseStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string? FailureReason
    {
        get => ErrorMessage;
        set => ErrorMessage = value;
    }
    public string IdempotencyHash { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
