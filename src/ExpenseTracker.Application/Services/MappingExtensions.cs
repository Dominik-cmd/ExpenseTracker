using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Services;

public static class MappingExtensions
{
  public static TransactionDto ToDto(this Transaction transaction)
  {
    return new TransactionDto(
      transaction.Id,
      transaction.UserId,
      transaction.Amount,
      transaction.Currency,
      transaction.Direction,
      transaction.TransactionType,
      transaction.TransactionDate,
      transaction.MerchantRaw,
      transaction.MerchantNormalized,
      transaction.CategoryId,
      transaction.CategorySource,
      transaction.TransactionSource,
      transaction.Notes,
      transaction.IsDeleted,
      transaction.RawMessageId,
      transaction.CreatedAt,
      transaction.UpdatedAt,
      transaction.Category.Name,
      transaction.Category.ParentCategory?.Name);
  }

  public static CategoryDto ToDto(this Category category)
  {
    return new CategoryDto(
      category.Id,
      category.Name,
      category.Color,
      category.Icon,
      category.SortOrder,
      category.IsSystem,
      category.ExcludeFromExpenses,
      category.ExcludeFromIncome,
      category.ParentCategoryId,
      category.SubCategories
        .OrderBy(x => x.SortOrder)
        .ThenBy(x => x.Name)
        .Select(ToDto)
        .ToList());
  }

  public static MerchantRuleDto ToDto(this MerchantRule rule)
  {
    return new MerchantRuleDto(
      rule.Id,
      rule.MerchantNormalized,
      rule.CategoryId,
      rule.Category.Name,
      rule.Category.ParentCategory?.Name,
      rule.CreatedBy,
      rule.HitCount,
      rule.LastHitAt,
      rule.CreatedAt);
  }

  public static RawMessageDto ToDto(this RawMessage message)
  {
    return new RawMessageDto(
      message.Id,
      message.Sender,
      message.Body,
      message.SentAt,
      message.ParseStatus,
      message.FailureReason,
      message.IdempotencyHash,
      message.Transactions
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => (Guid?)x.Id)
        .FirstOrDefault(),
      message.CreatedAt);
  }

  public static UserDto ToDto(this User user)
  {
    return new UserDto(user.Id, user.Username, user.IsAdmin, user.CreatedAt);
  }
}
