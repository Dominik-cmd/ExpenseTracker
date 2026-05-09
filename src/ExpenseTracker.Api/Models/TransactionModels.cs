using System;
using System.Collections.Generic;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Api.Models
{


public sealed record TransactionDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string Currency,
    Direction Direction,
    TransactionType TransactionType,
    DateTime TransactionDate,
    string? MerchantRaw,
    string? MerchantNormalized,
    Guid CategoryId,
    CategorySource CategorySource,
    TransactionSource TransactionSource,
    string? Notes,
    bool IsDeleted,
    Guid? RawMessageId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CategoryName,
    string? ParentCategoryName);

public sealed record CreateTransactionRequest(
    decimal Amount,
    Direction Direction,
    TransactionType TransactionType,
    DateTime TransactionDate,
    string? MerchantRaw,
    Guid CategoryId,
    string? Notes);

public sealed record UpdateTransactionRequest(
    decimal? Amount,
    string? Currency,
    Direction? Direction,
    TransactionType? TransactionType,
    DateTime? TransactionDate,
    string? MerchantRaw,
    Guid? CategoryId,
    string? Notes);

public sealed record TransactionFilterParams(
    DateTime? From,
    DateTime? To,
    Guid? CategoryId,
    List<Guid>? CategoryIds,
    string? Merchant,
    decimal? MinAmount,
    decimal? MaxAmount,
    Direction? Direction,
    TransactionSource? Source,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record RecategorizeRequest(Guid CategoryId, bool CreateMerchantRule);

public sealed record BulkRecategorizeRequest(List<Guid> TransactionIds, Guid CategoryId, bool CreateMerchantRule);
}

