using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Route("api/webhook")]
public sealed class WebhookController(IWebhookService webhookService) : ApiControllerBase
{
  [HttpPost("sms")]
  public async Task<ActionResult<WebhookSmsResponse>> ReceiveSmsAsync(
    [FromBody] WebhookSmsRequest request, CancellationToken ct)
  {
    var secret = Request.Headers["X-Webhook-Secret"].ToString();
    if (string.IsNullOrWhiteSpace(secret))
    {
      return Unauthorized();
    }
    var result = await webhookService.ProcessSmsAsync(secret, request, ct);
    if (result is null)
    {
      return Unauthorized();
    }
    return Ok(result);
  }
}
