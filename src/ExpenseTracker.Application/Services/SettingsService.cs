using System.Security.Cryptography;
using System.Text.Json;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Services;

public sealed class SettingsService(ISettingRepository settingRepository) : ISettingsService
{
  public async Task<string?> GetWebhookSecretAsync(Guid userId, CancellationToken ct)
  {
    var setting = await settingRepository.GetAsync(userId, "sms_webhook_secret", ct);
    return setting?.Value;
  }

  public async Task<string?> RotateWebhookSecretAsync(Guid userId, CancellationToken ct)
  {
    var existing = await settingRepository.GetAsync(userId, "sms_webhook_secret", ct);
    if (existing is null)
    {
      return null;
    }

    var secret = GenerateUrlSafeSecret();
    await settingRepository.UpsertAsync(userId, "sms_webhook_secret", secret, ct);
    await settingRepository.SaveChangesAsync(ct);
    return secret;
  }

  public async Task<List<string>?> GetSmsSendersAsync(Guid userId, CancellationToken ct)
  {
    var setting = await settingRepository.GetAsync(userId, "sms_senders", ct);
    if (setting?.Value is null)
    {
      return null;
    }

    return JsonSerializer.Deserialize<List<string>>(setting.Value);
  }

  public async Task<List<string>?> UpdateSmsSendersAsync(
    Guid userId, UpdateSmsSendersRequest request, CancellationToken ct)
  {
    var existing = await settingRepository.GetAsync(userId, "sms_senders", ct);
    if (existing is null)
    {
      return null;
    }

    var json = JsonSerializer.Serialize(request.Senders);
    await settingRepository.UpsertAsync(userId, "sms_senders", json, ct);
    await settingRepository.SaveChangesAsync(ct);
    return request.Senders;
  }

  private static string GenerateUrlSafeSecret()
  {
    Span<byte> bytes = stackalloc byte[32];
    RandomNumberGenerator.Fill(bytes);
    return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
  }
}
