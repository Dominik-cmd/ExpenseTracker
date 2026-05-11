using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(
  ITransactionService transactionService,
  ILogger<TransactionsController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetAsync(
      [FromQuery] TransactionFilterParams filter, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await transactionService.GetPagedAsync(userId.Value, filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await transactionService.GetByIdAsync(id, userId.Value, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateAsync(
      [FromBody] CreateTransactionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await transactionService.CreateAsync(userId.Value, request, ct);
        logger.LogInformation("Transaction {TransactionId} created by user {UserId}, amount {Amount}",
          result.Id, userId.Value, request.Amount);
        return Created($"/api/transactions/{result.Id}", result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> UpdateAsync(
      Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await transactionService.UpdateAsync(id, userId.Value, request, ct);
        if (result is null)
        {
            return NotFound();
        }
        logger.LogInformation("Transaction {TransactionId} updated by user {UserId}", id, userId.Value);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var deleted = await transactionService.DeleteAsync(id, userId.Value, ct);
        if (!deleted)
        {
            return NotFound();
        }
        logger.LogInformation("Transaction {TransactionId} deleted by user {UserId}", id, userId.Value);
        return NoContent();
    }

    [HttpPost("{id:guid}/recategorize")]
    public async Task<ActionResult<TransactionDto>> RecategorizeAsync(
      Guid id, [FromBody] RecategorizeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await transactionService.RecategorizeAsync(id, userId.Value, request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("bulk-recategorize")]
    public async Task<IActionResult> BulkRecategorizeAsync(
      [FromBody] BulkRecategorizeRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await transactionService.BulkRecategorizeAsync(userId.Value, request, ct);
        logger.LogInformation("Bulk recategorize by user {UserId}: {Count} transactions",
          userId.Value, request.TransactionIds?.Count ?? 0);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync(
      [FromQuery] TransactionFilterParams filter, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var csvBytes = await transactionService.ExportCsvAsync(userId.Value, filter, ct);
        logger.LogInformation("CSV export by user {UserId}, size {Bytes} bytes", userId.Value, csvBytes.Length);
        return File(csvBytes, "text/csv", $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
