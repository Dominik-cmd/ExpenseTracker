using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Investments;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/investment-providers")]
public sealed class InvestmentProvidersController(
    AppDbContext dbContext,
    IbkrFlexProvider ibkrProvider,
    ManualInvestmentProvider manualProvider,
    IDataProtectionProvider dataProtection,
    ILogger<InvestmentProvidersController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var protector = dataProtection.CreateProtector("InvestmentApiKeys");
        var providers = await dbContext.InvestmentProviders
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value)
            .ToListAsync(ct);

        return Ok(providers.Select(p => new
        {
            p.Id,
            providerType = p.ProviderType.ToString().ToLowerInvariant(),
            p.DisplayName,
            token = DecryptSafe(p.ApiTokenEncrypted, protector),
            extraConfig = p.ExtraConfig is not null ? DeserializeConfig(p.ExtraConfig) : null,
            p.IsEnabled,
            p.LastSyncAt,
            p.LastSyncStatus,
            p.LastSyncError,
            p.LastTestAt,
            p.LastTestStatus,
            p.LastTestError,
            p.CreatedAt,
            p.UpdatedAt
        }));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateProvider(Guid id, [FromBody] UpdateInvestmentProviderRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value, ct);
        if (provider is null) return NotFound();

        if (request.DisplayName is not null)
            provider.DisplayName = request.DisplayName;

        if (request.ApiToken is not null)
        {
            var protector = dataProtection.CreateProtector("InvestmentApiKeys");
            provider.ApiTokenEncrypted = protector.Protect(request.ApiToken);
        }

        if (request.ExtraConfig is not null)
        {
            var existing = provider.ExtraConfig is not null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(provider.ExtraConfig) ?? new()
                : new Dictionary<string, JsonElement>();

            var incoming = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.ExtraConfig.Value.GetRawText()) ?? new();
            foreach (var kvp in incoming)
                existing[kvp.Key] = kvp.Value;

            provider.ExtraConfig = JsonSerializer.Serialize(existing);
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestProvider(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value, ct);
        if (provider is null) return NotFound();

        ExpenseTracker.Core.Investments.ProviderTestResult result;
        if (provider.ProviderType == InvestmentProviderType.Ibkr)
            result = await ibkrProvider.TestAsync(id, ct);
        else
            result = await manualProvider.TestAsync(id, ct);

        provider.LastTestAt = DateTime.UtcNow;
        provider.LastTestStatus = result.Success ? "success" : "failure";
        provider.LastTestError = result.Success ? null : result.Message;
        provider.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { result.Success, result.Message, latencyMs = (int)result.Latency.TotalMilliseconds });
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<IActionResult> EnableProvider(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value, ct);
        if (provider is null) return NotFound();

        provider.IsEnabled = true;
        provider.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> DisableProvider(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var provider = await dbContext.InvestmentProviders
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId.Value, ct);
        if (provider is null) return NotFound();

        provider.IsEnabled = false;
        provider.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }

    private static string? DecryptSafe(string? encrypted, IDataProtector protector)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try { return protector.Unprotect(encrypted); }
        catch { return null; }
    }

    private static object? DeserializeConfig(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json); }
        catch { return null; }
    }
}

public record UpdateInvestmentProviderRequest(string? DisplayName, string? ApiToken, JsonElement? ExtraConfig);

}
