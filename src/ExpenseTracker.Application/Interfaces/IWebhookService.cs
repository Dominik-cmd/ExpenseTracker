using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IWebhookService
{
  Task<WebhookSmsResponse?> ProcessSmsAsync(string secret, WebhookSmsRequest request, CancellationToken ct);
}
