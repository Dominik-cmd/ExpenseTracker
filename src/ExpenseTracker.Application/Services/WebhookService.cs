using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Services;

public sealed class WebhookService(
  ISettingRepository settingRepository,
  IRawMessageRepository rawMessageRepository,
  Channel<Guid> smsChannel,
  ILogger<WebhookService> logger) : IWebhookService
{
    public async Task<WebhookSmsResponse?> ProcessSmsAsync(
      string secret, WebhookSmsRequest request, CancellationToken ct)
    {
        var userId = await ResolveUserBySecretAsync(secret, ct);
        if (userId is null)
        {
            logger.LogWarning("SMS webhook: no user found for provided secret");
            return null;
        }

        var hash = ComputeIdempotencyHash(userId.Value, request.From, request.Text, request.SentStamp);
        if (await rawMessageRepository.ExistsByHashAsync(hash, ct))
        {
            logger.LogDebug("SMS webhook: duplicate message detected for user {UserId}", userId.Value);
            return new WebhookSmsResponse("duplicate");
        }

        DateTime receivedAt;
        if (!string.IsNullOrWhiteSpace(request.SentStamp)
          && DateTime.TryParse(request.SentStamp, out var parsed))
        {
            receivedAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        else
        {
            receivedAt = DateTime.UtcNow;
        }

        var message = new RawMessage
        {
            UserId = userId.Value,
            Sender = request.From,
            Body = request.Text,
            ReceivedAt = receivedAt,
            IdempotencyHash = hash,
            ParseStatus = ParseStatus.Pending
        };

        await rawMessageRepository.AddAsync(message, ct);
        await rawMessageRepository.SaveChangesAsync(ct);
        await smsChannel.Writer.WriteAsync(message.Id, ct);

        logger.LogInformation("SMS queued for processing: {MessageId} from {Sender} for user {UserId}",
          message.Id, request.From, userId.Value);
        return new WebhookSmsResponse("accepted");
    }

    private async Task<Guid?> ResolveUserBySecretAsync(string secret, CancellationToken ct)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var allSecrets = await settingRepository.GetAllByKeyAsync("sms_webhook_secret", ct);
        foreach (var setting in allSecrets)
        {
            if (setting.Value is null)
            {
                continue;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(setting.Value);
            if (expectedBytes.Length == secretBytes.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, secretBytes))
            {
                return setting.UserId;
            }
        }

        return null;
    }

    private static string ComputeIdempotencyHash(Guid userId, string from, string text, string? sentStamp)
    {
        var input = $"{userId}|{from}|{text}|{sentStamp ?? ""}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
