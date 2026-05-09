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
    [HttpGet]
    public async Task<ActionResult<List<RawMessageDto>>> GetAsync([FromQuery] ParseStatus? status, CancellationToken ct)
    {
        try
        {
            var query = dbContext.RawMessages
                .AsNoTracking()
                .Include(x => x.Transactions)
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
            var rawMessage = await dbContext.RawMessages.Include(x => x.Transactions).FirstOrDefaultAsync(x => x.Id == id, ct);
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
            var rawMessage = await dbContext.RawMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
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

