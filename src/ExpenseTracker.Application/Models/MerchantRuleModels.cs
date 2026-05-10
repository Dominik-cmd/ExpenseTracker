namespace ExpenseTracker.Application.Models;

public sealed record MerchantRuleDto(
  Guid Id,
  string MerchantNormalized,
  Guid CategoryId,
  string CategoryName,
  string? ParentCategoryName,
  string CreatedBy,
  int HitCount,
  DateTime? LastHitAt,
  DateTime CreatedAt);

public sealed record UpdateMerchantRuleRequest(Guid CategoryId, bool ApplyToExistingTransactions = false);
