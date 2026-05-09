using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(AppDbContext dbContext, ILogger<TransactionsController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetAsync([FromQuery] TransactionFilterParams filter, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var page = Math.Max(filter.Page, 1);
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);
            var query = BuildQuery(userId.Value, filter);
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new PagedResult<TransactionDto>(items.Select(x => x.ToDto()).ToList(), totalCount, page, pageSize));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch transactions.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch transactions.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var transaction = await FindTransactionAsync(id, ct);
            return transaction is null ? NotFound() : Ok(transaction.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch transaction {TransactionId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch transaction.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateAsync([FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, ct))
            {
                return BadRequest(new { message = "Category does not exist." });
            }

            var transaction = new Transaction
            {
                UserId = userId.Value,
                Amount = request.Amount,
                Currency = "EUR",
                Direction = request.Direction,
                TransactionType = request.TransactionType,
                TransactionDate = request.TransactionDate,
                MerchantRaw = request.MerchantRaw ?? string.Empty,
                MerchantNormalized = MerchantNormalizer.Normalize(request.MerchantRaw ?? string.Empty),
                CategoryId = request.CategoryId,
                CategorySource = CategorySource.Manual,
                TransactionSource = TransactionSource.Manual,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync(ct);
            var created = await FindTransactionAsync(transaction.Id, ct);
            return Created($"/api/transactions/{transaction.Id}", created!.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create transaction.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to create transaction.");
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> UpdateAsync(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var transaction = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value && !x.IsDeleted, ct);
            if (transaction is null) return NotFound();

            var changes = new Dictionary<string, object?>();
            if (request.Amount.HasValue) { transaction.Amount = request.Amount.Value; changes[nameof(transaction.Amount)] = request.Amount.Value; }
            if (!string.IsNullOrWhiteSpace(request.Currency)) { transaction.Currency = request.Currency.Trim().ToUpperInvariant(); changes[nameof(transaction.Currency)] = transaction.Currency; }
            if (request.Direction.HasValue) { transaction.Direction = request.Direction.Value; changes[nameof(transaction.Direction)] = request.Direction.Value; }
            if (request.TransactionType.HasValue) { transaction.TransactionType = request.TransactionType.Value; changes[nameof(transaction.TransactionType)] = request.TransactionType.Value; }
            if (request.TransactionDate.HasValue) { transaction.TransactionDate = request.TransactionDate.Value; changes[nameof(transaction.TransactionDate)] = request.TransactionDate.Value; }
            if (request.MerchantRaw is not null) { transaction.MerchantRaw = request.MerchantRaw; transaction.MerchantNormalized = MerchantNormalizer.Normalize(request.MerchantRaw); changes[nameof(transaction.MerchantRaw)] = request.MerchantRaw; }
            if (request.CategoryId.HasValue)
            {
                if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId.Value, ct))
                {
                    return BadRequest(new { message = "Category does not exist." });
                }

                transaction.CategoryId = request.CategoryId.Value;
                transaction.CategorySource = CategorySource.Manual;
                changes[nameof(transaction.CategoryId)] = request.CategoryId.Value;
            }
            if (request.Notes is not null) { transaction.Notes = request.Notes; changes[nameof(transaction.Notes)] = request.Notes; }

            transaction.UpdatedAt = DateTime.UtcNow;
            AddAuditLog(nameof(Transaction), transaction.Id, "Patch", changes, userId.Value);
            await dbContext.SaveChangesAsync(ct);
            var updated = await FindTransactionAsync(transaction.Id, ct);
            return Ok(updated!.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update transaction {TransactionId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to update transaction.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var transaction = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value && !x.IsDeleted, ct);
            if (transaction is null) return NotFound();

            transaction.IsDeleted = true;
            transaction.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete transaction {TransactionId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to delete transaction.");
        }
    }

    [HttpPost("{id:guid}/recategorize")]
    public async Task<ActionResult<TransactionDto>> RecategorizeAsync(Guid id, [FromBody] RecategorizeRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var transaction = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value && !x.IsDeleted, ct);
            if (transaction is null) return NotFound();
            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, ct)) return BadRequest(new { message = "Category does not exist." });

            transaction.CategoryId = request.CategoryId;
            transaction.CategorySource = CategorySource.Manual;
            transaction.UpdatedAt = DateTime.UtcNow;

            if (request.CreateMerchantRule && !string.IsNullOrWhiteSpace(transaction.MerchantNormalized))
            {
                await UpsertMerchantRuleAsync(transaction.MerchantNormalized, request.CategoryId, "manual", ct);
            }

            await dbContext.SaveChangesAsync(ct);
            var updated = await FindTransactionAsync(transaction.Id, ct);
            return Ok(updated!.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to recategorize transaction {TransactionId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to recategorize transaction.");
        }
    }

    [HttpPost("bulk-recategorize")]
    public async Task<IActionResult> BulkRecategorizeAsync([FromBody] BulkRecategorizeRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();
            if (!await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId, ct)) return BadRequest(new { message = "Category does not exist." });

            var transactions = await dbContext.Transactions
                .Where(x => request.TransactionIds.Contains(x.Id) && x.UserId == userId.Value && !x.IsDeleted)
                .ToListAsync(ct);

            foreach (var transaction in transactions)
            {
                transaction.CategoryId = request.CategoryId;
                transaction.CategorySource = CategorySource.Manual;
                transaction.UpdatedAt = DateTime.UtcNow;
                if (request.CreateMerchantRule && !string.IsNullOrWhiteSpace(transaction.MerchantNormalized))
                {
                    await UpsertMerchantRuleAsync(transaction.MerchantNormalized, request.CategoryId, "manual", ct);
                }
            }

            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk recategorize transactions.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to recategorize transactions.");
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] TransactionFilterParams filter, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var items = await BuildQuery(userId.Value, filter)
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync(ct);

            await using var stream = new MemoryStream();
            await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(items.Select(x => x.ToDto()), ct);
            await writer.FlushAsync();
            stream.Position = 0;
            return File(stream.ToArray(), "text/csv", $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export transactions.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to export transactions.");
        }
    }

    private IQueryable<Transaction> BuildQuery(Guid userId, TransactionFilterParams filter)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Include(x => x.Category)
            .ThenInclude(x => x.ParentCategory)
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (filter.From.HasValue) query = query.Where(x => x.TransactionDate >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(x => x.TransactionDate <= filter.To.Value);
        if (filter.CategoryId.HasValue) query = query.Where(x => x.CategoryId == filter.CategoryId || x.Category.ParentCategoryId == filter.CategoryId);
        if (filter.CategoryIds != null && filter.CategoryIds.Count > 0) query = query.Where(x => filter.CategoryIds.Contains(x.CategoryId) || (x.Category.ParentCategoryId.HasValue && filter.CategoryIds.Contains(x.Category.ParentCategoryId.Value)));
        if (!string.IsNullOrWhiteSpace(filter.Merchant))
        {
            var merchant = filter.Merchant.Trim().ToUpperInvariant();
            query = query.Where(x => (x.MerchantRaw + " " + x.MerchantNormalized).ToUpper().Contains(merchant));
        }
        if (filter.MinAmount.HasValue) query = query.Where(x => x.Amount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue) query = query.Where(x => x.Amount <= filter.MaxAmount.Value);
        if (filter.Direction.HasValue) query = query.Where(x => x.Direction == filter.Direction.Value);
        if (filter.Source.HasValue) query = query.Where(x => x.TransactionSource == filter.Source.Value);
        return query;
    }

    private async Task<Transaction?> FindTransactionAsync(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return null;

        return await dbContext.Transactions
            .AsNoTracking()
            .Include(x => x.Category)
            .ThenInclude(x => x.ParentCategory)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value && !x.IsDeleted, ct);
    }

    private void AddAuditLog(string entityType, Guid entityId, string action, object changes, Guid userId)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Action = action,
            ChangesJson = JsonSerializer.Serialize(changes),
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task UpsertMerchantRuleAsync(string merchantNormalized, Guid categoryId, string createdBy, CancellationToken ct)
    {
        var rule = await dbContext.MerchantRules.FirstOrDefaultAsync(x => x.MerchantNormalized == merchantNormalized, ct);
        if (rule is null)
        {
            dbContext.MerchantRules.Add(new MerchantRule
            {
                MerchantNormalized = merchantNormalized,
                CategoryId = categoryId,
                CreatedBy = createdBy,
                HitCount = 0,
                LastHitAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        rule.CategoryId = categoryId;
        rule.LastHitAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
    }
}
}

