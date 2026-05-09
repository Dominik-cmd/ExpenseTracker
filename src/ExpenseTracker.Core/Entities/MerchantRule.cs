namespace ExpenseTracker.Core.Entities;

public class MerchantRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string MerchantNormalized { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string CreatedBy { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public DateTime? LastHitAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
