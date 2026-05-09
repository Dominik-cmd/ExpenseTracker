using System.Text.Json;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Core.Records;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/llm-providers")]
public sealed class LlmProvidersController(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    ILlmProviderResolver providerResolver,
    IEnumerable<ILlmCategorizationProvider> providers,
    ILogger<LlmProvidersController> logger) : ApiControllerBase
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("LlmApiKeys");

    [HttpGet]
    public async Task<ActionResult<List<LlmProviderDto>>> GetAsync(CancellationToken ct)
    {
        try
        {
            var items = await dbContext.LlmProviders.AsNoTracking().OrderBy(x => x.ProviderType).ToListAsync(ct);
            return Ok(items.Select(x => x.ToDto()).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch LLM providers.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch LLM providers.");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LlmProviderDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var provider = await dbContext.LlmProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return provider is null ? NotFound() : Ok(provider.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch LLM provider {ProviderId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch LLM provider.");
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<LlmProviderDto>> UpdateAsync(Guid id, [FromBody] UpdateLlmProviderRequest request, CancellationToken ct)
    {
        try
        {
            var provider = await dbContext.LlmProviders.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (provider is null) return NotFound();

            var changes = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(request.Model))
            {
                provider.Model = request.Model.Trim();
                changes[nameof(provider.Model)] = provider.Model;
            }

            if (request.ApiKey is not null)
            {
                provider.ApiKeyEncrypted = string.IsNullOrWhiteSpace(request.ApiKey) ? null : _protector.Protect(request.ApiKey.Trim());
                changes["ApiKeyUpdated"] = true;
            }

            provider.UpdatedAt = DateTime.UtcNow;
            AddAuditLog(provider.Id, "Patch", changes);
            await dbContext.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();
            return Ok(provider.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update LLM provider {ProviderId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to update LLM provider.");
        }
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> EnableAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var providersToUpdate = await dbContext.LlmProviders.ToListAsync(ct);
            if (providersToUpdate.All(x => x.Id != id)) return NotFound();

            foreach (var provider in providersToUpdate)
            {
                provider.IsEnabled = provider.Id == id;
                provider.UpdatedAt = DateTime.UtcNow;
            }

            AddAuditLog(id, "Enable", new { id });
            await dbContext.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable LLM provider {ProviderId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to enable LLM provider.");
        }
    }

    [HttpPost("disable-all")]
    public async Task<IActionResult> DisableAllAsync(CancellationToken ct)
    {
        try
        {
            var providersToUpdate = await dbContext.LlmProviders.ToListAsync(ct);
            foreach (var provider in providersToUpdate)
            {
                provider.IsEnabled = false;
                provider.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable all LLM providers.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to disable LLM providers.");
        }
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<LlmTestResponse>> TestAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var providerRow = await dbContext.LlmProviders.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (providerRow is null) return NotFound();
            if (string.IsNullOrWhiteSpace(providerRow.ApiKeyEncrypted))
            {
                providerRow.LastTestedAt = DateTime.UtcNow;
                providerRow.LastTestStatus = LlmTestStatus.Failed;
                await dbContext.SaveChangesAsync(ct);
                return Ok(new LlmTestResponse(false, 0, "API key is not configured."));
            }

            var provider = providers.FirstOrDefault(x => x.ProviderType == providerRow.ProviderType);
            if (provider is null) return NotFound();

            var categories = await dbContext.Categories.AsNoTracking().ToListAsync(ct);
            var startedAt = DateTime.UtcNow;
            var result = await provider.CategorizeAsync(
                providerRow,
                new CategorizationRequest("NETFLIX", "NETFLIX", 12.99m, Direction.Debit, TransactionType.Purchase, "TEST REQUEST"),
                categories,
                ct);
            var latencyMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

            providerRow.LastTestedAt = DateTime.UtcNow;
            providerRow.LastTestStatus = result is null ? LlmTestStatus.Failed : LlmTestStatus.Success;
            await dbContext.SaveChangesAsync(ct);
            return Ok(new LlmTestResponse(result is not null, latencyMs, result is null ? "Provider test did not return a result." : null));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to test LLM provider {ProviderId}.", id);
            var providerRow = await dbContext.LlmProviders.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (providerRow is not null)
            {
                providerRow.LastTestedAt = DateTime.UtcNow;
                providerRow.LastTestStatus = LlmTestStatus.Failed;
                await dbContext.SaveChangesAsync(ct);
            }

            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to test LLM provider.");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<LlmProviderDto?>> GetActiveAsync(CancellationToken ct)
    {
        try
        {
            var provider = await dbContext.LlmProviders.AsNoTracking().FirstOrDefaultAsync(x => x.IsEnabled, ct);
            return Ok(provider?.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch active LLM provider.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch active LLM provider.");
        }
    }

    private void AddAuditLog(Guid entityId, string action, object changes)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityType = nameof(LlmProvider),
            EntityId = entityId.ToString(),
            Action = action,
            ChangesJson = JsonSerializer.Serialize(changes),
            UserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
}

