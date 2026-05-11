using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[Route("api/webhooks")]
public sealed class WebhookController(
  IWebhookService webhookService,
  ILogger<WebhookController> logger) : ApiControllerBase
{
  [HttpPost("sms")]
  [EnableRateLimiting(RateLimitingMiddleware.WebhookPolicyName)]
  public async Task<ActionResult<WebhookSmsResponse>> ReceiveSmsAsync(
    [FromBody] WebhookSmsRequest request, CancellationToken ct)
  {
    var secret = Request.Headers["X-Webhook-Secret"].ToString();
    if (string.IsNullOrWhiteSpace(secret))
    {
      logger.LogWarning("SMS webhook received without secret header from {RemoteIp}",
        HttpContext.Connection.RemoteIpAddress);
      return Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
    {
      return BadRequest(new { message = "Text must be between 1 and 2000 characters." });
    }

    if (string.IsNullOrWhiteSpace(request.From) || request.From.Length > 100)
    {
      return BadRequest(new { message = "From must be between 1 and 100 characters." });
    }

    var result = await webhookService.ProcessSmsAsync(secret, request, ct);
    if (result is null)
    {
      logger.LogWarning("SMS webhook authentication failed from {RemoteIp}",
        HttpContext.Connection.RemoteIpAddress);
      return Unauthorized();
    }

    logger.LogInformation("SMS webhook processed: {Status} from {Sender}",
      result.Status, request.From);
    return Ok(result);
  }
}
