using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Records;

namespace ExpenseTracker.Application.Models;

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

public sealed record QueuedItemDto(Guid Id, string Preview, DateTime CreatedAt);

public sealed record RecentItemDto(Guid Id, string Preview, string Status, string? FailureReason, DateTime ProcessedAt);

public sealed record QueueStatusDto(int PendingCount, List<QueuedItemDto> Pending, List<RecentItemDto> RecentlyProcessed);

public sealed record WebhookSmsRequest(string From, string Text, string? SentStamp);

public sealed record WebhookSmsResponse(string Status);
