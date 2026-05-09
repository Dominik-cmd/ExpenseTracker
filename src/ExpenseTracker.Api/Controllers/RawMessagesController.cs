using System.Threading.Channels;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/raw-messages")]
public sealed class RawMessagesController(AppDbContext dbContext, Channel<Guid> channel, ILogger<RawMessagesController> logger) : ApiControllerBase
{
    [HttpGet("queue-status")]
    public async Task<ActionResult<QueueStatusDto>> GetQueueStatusAsync(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var pending = await dbContext.RawMessages
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.ParseStatus == ParseStatus.Pending)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new QueuedItemDto(x.Id, x.Body.Length > 80 ? x.Body.Substring(0, 80) + "…" : x.Body, x.CreatedAt))
                .ToListAsync(ct);

            var recentlyProcessed = await dbContext.RawMessages
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value && x.ParseStatus != ParseStatus.Pending)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(20)
                .Select(x => new RecentItemDto(x.Id, x.Body.Length > 80 ? x.Body.Substring(0, 80) + "…" : x.Body, x.ParseStatus.ToString(), x.FailureReason, x.UpdatedAt))
                .ToListAsync(ct);

            return Ok(new QueueStatusDto(pending.Count, pending, recentlyProcessed));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch queue status.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch queue status.");
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<RawMessageDto>>> GetAsync([FromQuery] ParseStatus? status, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var query = dbContext.RawMessages
                .AsNoTracking()
                .Include(x => x.Transactions)
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(x => x.ParseStatus == status.Value);
            }

            var items = await query.ToListAsync(ct);
            return Ok(items.Select(x => x.ToDto()).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch raw messages.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch raw messages.");
        }
    }

    [HttpPost("{id:guid}/reprocess")]
    public async Task<IActionResult> ReprocessAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var rawMessage = await dbContext.RawMessages.Include(x => x.Transactions).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
            if (rawMessage is null) return NotFound();

            if (rawMessage.Transactions.Count != 0)
            {
                dbContext.Transactions.RemoveRange(rawMessage.Transactions);
            }

            rawMessage.ParseStatus = ParseStatus.Pending;
            rawMessage.FailureReason = null;
            rawMessage.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            await channel.Writer.WriteAsync(rawMessage.Id, ct);
            return Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reprocess raw message {RawMessageId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to reprocess raw message.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var rawMessage = await dbContext.RawMessages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, ct);
            if (rawMessage is null) return NotFound();
            dbContext.RawMessages.Remove(rawMessage);
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete raw message {RawMessageId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to delete raw message.");
        }
    }
}
}

