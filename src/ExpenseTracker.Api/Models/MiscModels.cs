using System;
using System.Collections.Generic;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Records;

namespace ExpenseTracker.Api.Models
{


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

public sealed record RawMessageDto(
    Guid Id,
    string Sender,
    string Body,
    DateTime ReceivedAt,
    ParseStatus ParseStatus,
    string? ErrorMessage,
    string IdempotencyHash,
    Guid? TransactionId,
    DateTime CreatedAt);

public sealed record UpdateSmsSendersRequest(List<string> Senders);

public sealed record DiagnosticParseRequest(string Text);

public sealed record DiagnosticParseResponse(bool Success, ParsedSms? ParsedSms, string? ErrorMessage);
}

