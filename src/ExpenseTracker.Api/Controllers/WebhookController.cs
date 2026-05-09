using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Route("api/webhooks")]
public sealed class WebhookController(AppDbContext dbContext, Channel<Guid> channel, ILogger<WebhookController> logger) : ApiControllerBase
{
    [HttpPost("sms")]
    public async Task<ActionResult<SmsWebhookResponse>> ReceiveSmsAsync([FromBody] SmsWebhookRequest request, CancellationToken ct)
    {
        try
        {
            var secret = await dbContext.Settings.AsNoTracking().Where(x => x.Key == "sms_webhook_secret").Select(x => x.Value).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(secret))
            {
                return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Webhook secret is not configured.");
            }

            if (!Request.Headers.TryGetValue("X-Webhook-Secret", out var provided) || !SecretsMatch(secret, provided.ToString()))
            {
                return Unauthorized();
            }

            var idempotencyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.From}|{request.Text}|{request.SentStamp}")));
            if (await dbContext.RawMessages.AnyAsync(x => x.IdempotencyHash == idempotencyHash, ct))
            {
                return Ok(new SmsWebhookResponse("duplicate"));
            }

            var sentAt = DateTime.TryParse(request.SentStamp, out var parsedSentAt) ? parsedSentAt.ToUniversalTime() : DateTime.UtcNow;
            var rawMessage = new RawMessage
            {
                Sender = request.From,
                Body = request.Text,
                SentAt = sentAt,
                IdempotencyHash = idempotencyHash,
                ParseStatus = ParseStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.RawMessages.Add(rawMessage);
            await dbContext.SaveChangesAsync(ct);
            await channel.Writer.WriteAsync(rawMessage.Id, ct);
            return Ok(new SmsWebhookResponse("accepted"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS webhook processing failed.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to process SMS webhook.");
        }
    }

    private static bool SecretsMatch(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
}

