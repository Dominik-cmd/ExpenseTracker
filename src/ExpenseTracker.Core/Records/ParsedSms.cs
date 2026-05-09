using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Records;

public sealed record ParsedSms(
    Direction Direction,
    TransactionType TransactionType,
    decimal Amount,
    string Currency,
    DateTime TransactionDate,
    string MerchantRaw,
    string MerchantNormalized,
    string? Notes);
