using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Records;

public sealed record CategorizationRequest
{
    public CategorizationRequest(string merchantName, decimal amount, TransactionType transactionType, string? purpose, IReadOnlyCollection<CategoryHierarchy> categories)
    {
        MerchantRaw = merchantName;
        MerchantNormalized = merchantName;
        Amount = amount;
        Direction = null;
        TransactionType = transactionType;
        Notes = purpose;
        Categories = categories;
    }

    public CategorizationRequest(string merchantRaw, string merchantNormalized, decimal amount, Direction direction, TransactionType transactionType, string? notes)
    {
        MerchantRaw = merchantRaw;
        MerchantNormalized = merchantNormalized;
        Amount = amount;
        Direction = direction;
        TransactionType = transactionType;
        Notes = notes;
        Categories = Array.Empty<CategoryHierarchy>();
    }

    public Guid UserId { get; init; }
    public string MerchantRaw { get; }
    public string MerchantNormalized { get; }
    public decimal Amount { get; }
    public Direction? Direction { get; }
    public TransactionType TransactionType { get; }
    public string? Notes { get; }
    public IReadOnlyCollection<CategoryHierarchy> Categories { get; }
}
