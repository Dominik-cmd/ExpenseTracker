using ExpenseTracker.Api.Models;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Api.Services
{


public static class ApiMappingExtensions
{
    public static TransactionDto ToDto(this Transaction transaction)
        => new(
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

    public static CategoryDto ToDto(this Category category)
        => new(
            category.Id,
            category.Name,
            category.Color,
            category.Icon,
            category.SortOrder,
            category.IsSystem,
            category.ParentCategoryId,
            category.SubCategories.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(ToDto).ToList());

    public static MerchantRuleDto ToDto(this MerchantRule rule)
        => new(
            rule.Id,
            rule.MerchantNormalized,
            rule.CategoryId,
            rule.Category.Name,
            rule.Category.ParentCategory?.Name,
            rule.CreatedBy,
            rule.HitCount,
            rule.LastHitAt,
            rule.CreatedAt);

    public static RawMessageDto ToDto(this RawMessage message)
        => new(
            message.Id,
            message.Sender,
            message.Body,
            message.SentAt,
            message.ParseStatus,
            message.FailureReason,
            message.IdempotencyHash,
            message.Transactions.OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefault(),
            message.CreatedAt);

    public static LlmProviderDto ToDto(this LlmProvider provider)
        => new(
            provider.Id,
            provider.ProviderType.ToString(),
            string.IsNullOrWhiteSpace(provider.Name) ? provider.ProviderType.ToString() : provider.Name,
            provider.Model,
            provider.IsEnabled,
            !string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted),
            provider.LastTestedAt,
            provider.LastTestStatus?.ToString());
}
}

