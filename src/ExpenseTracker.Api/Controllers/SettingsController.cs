using System.Security.Cryptography;
using System.Text.Json;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/settings")]
public sealed class SettingsController(AppDbContext dbContext, ILogger<SettingsController> logger) : ApiControllerBase
{
    [HttpGet("webhook-secret")]
    public async Task<ActionResult<object>> GetWebhookSecretAsync(CancellationToken ct)
    {
        try
        {
            var secret = await GetSettingAsync("sms_webhook_secret", ct);
            return secret is null ? NotFound() : Ok(new { secret = secret.Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch webhook secret.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch webhook secret.");
        }
    }

    [HttpPost("webhook-secret/rotate")]
    public async Task<ActionResult<object>> RotateWebhookSecretAsync(CancellationToken ct)
    {
        try
        {
            var setting = await GetSettingAsync("sms_webhook_secret", ct);
            if (setting is null)
            {
                return NotFound();
            }

            setting.Value = CreateUrlSafeSecret();
            setting.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return Ok(new { secret = setting.Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rotate webhook secret.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to rotate webhook secret.");
        }
    }

    [HttpGet("sms-senders")]
    public async Task<ActionResult<List<string>>> GetSmsSendersAsync(CancellationToken ct)
    {
        try
        {
            var setting = await GetSettingAsync("sms_senders", ct);
            return setting is null || string.IsNullOrWhiteSpace(setting.Value)
                ? NotFound()
                : Ok(JsonSerializer.Deserialize<List<string>>(setting.Value) ?? []);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch SMS senders.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch SMS senders.");
        }
    }

    [HttpPatch("sms-senders")]
    public async Task<ActionResult<List<string>>> UpdateSmsSendersAsync([FromBody] UpdateSmsSendersRequest request, CancellationToken ct)
    {
        try
        {
            var setting = await GetSettingAsync("sms_senders", ct);
            if (setting is null)
            {
                return NotFound();
            }

            var cleaned = request.Senders
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            setting.Value = JsonSerializer.Serialize(cleaned);
            setting.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return Ok(cleaned);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update SMS senders.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to update SMS senders.");
        }
    }

    private Task<ExpenseTracker.Core.Entities.Setting?> GetSettingAsync(string key, CancellationToken ct)
        => dbContext.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);

    private static string CreateUrlSafeSecret()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
}

