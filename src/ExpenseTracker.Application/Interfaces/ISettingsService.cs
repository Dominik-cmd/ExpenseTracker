using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface ISettingsService
{
  Task<string?> GetWebhookSecretAsync(Guid userId, CancellationToken ct);
  Task<string?> RotateWebhookSecretAsync(Guid userId, CancellationToken ct);
  Task<List<string>?> GetSmsSendersAsync(Guid userId, CancellationToken ct);
  Task<List<string>?> UpdateSmsSendersAsync(Guid userId, UpdateSmsSendersRequest request, CancellationToken ct);
}
