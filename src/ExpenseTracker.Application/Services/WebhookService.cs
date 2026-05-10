using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Services;

public sealed class WebhookService(
  ISettingRepository settingRepository,
  IUserRepository userRepository,
  IRawMessageRepository rawMessageRepository,
  Channel<Guid> smsChannel) : IWebhookService
{
  public async Task<WebhookSmsResponse?> ProcessSmsAsync(
    string secret, WebhookSmsRequest request, CancellationToken ct)
  {
    var userId = await ResolveUserBySecretAsync(secret, ct);
    if (userId is null)
    {
      return null;
    }

    var hash = ComputeIdempotencyHash(request.From, request.Text, request.SentStamp);
    if (await rawMessageRepository.ExistsByHashAsync(hash, ct))
    {
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

    return new WebhookSmsResponse("queued");
  }

  private async Task<Guid?> ResolveUserBySecretAsync(string secret, CancellationToken ct)
  {
    var users = await userRepository.GetAllAsync(ct);
    foreach (var user in users)
    {
      var setting = await settingRepository.GetAsync(user.Id, "sms_webhook_secret", ct);
      if (setting?.Value == secret)
      {
        return user.Id;
      }
    }

    return null;
  }

  private static string ComputeIdempotencyHash(string from, string text, string? sentStamp)
  {
    var input = $"{from}|{text}|{sentStamp ?? ""}";
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(bytes).ToLowerInvariant();
  }
}
