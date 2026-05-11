using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/settings")]
public sealed class SettingsController(ISettingsService settingsService) : ApiControllerBase
{
    [HttpGet("webhook-secret")]
    public async Task<IActionResult> GetWebhookSecretAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var secret = await settingsService.GetWebhookSecretAsync(userId.Value, ct);
        if (secret is null)
        {
            return NotFound();
        }
        return Ok(new { secret });
    }

    [HttpPost("webhook-secret/rotate")]
    public async Task<IActionResult> RotateWebhookSecretAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var secret = await settingsService.RotateWebhookSecretAsync(userId.Value, ct);
        if (secret is null)
        {
            return NotFound();
        }
        return Ok(new { secret });
    }

    [HttpGet("sms-senders")]
    public async Task<IActionResult> GetSmsSendersAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var senders = await settingsService.GetSmsSendersAsync(userId.Value, ct);
        if (senders is null)
        {
            return NotFound();
        }
        return Ok(senders);
    }

    [HttpPut("sms-senders")]
    public async Task<IActionResult> UpdateSmsSendersAsync(
      [FromBody] UpdateSmsSendersRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }
        var result = await settingsService.UpdateSmsSendersAsync(userId.Value, request, ct);
        if (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }
}
