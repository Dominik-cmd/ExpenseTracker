using System.Text.Json;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class InvestmentProviderService(
  AppDbContext dbContext,
  IDataProtectionProvider dataProtectionProvider,
  IbkrFlexProvider ibkrFlexProvider,
  ILogger<InvestmentProviderService> logger) : IInvestmentProviderService
{
  private IDataProtector Protector => dataProtectionProvider.CreateProtector("InvestmentApiKeys.V2");

  public async Task<object> GetProvidersAsync(Guid userId, CancellationToken ct)
  {
    var providers = await dbContext.InvestmentProviders
      .AsNoTracking()
      .Where(p => p.UserId == userId)
      .ToListAsync(ct);

    return providers.Select(p =>
    {
      string? maskedToken = null;
      if (!string.IsNullOrWhiteSpace(p.ApiTokenEncrypted))
      {
        try
        {
          var raw = Protector.Unprotect(p.ApiTokenEncrypted);
          maskedToken = raw.Length > 4
            ? new string('*', raw.Length - 4) + raw[^4..]
            : new string('*', raw.Length);
        }
        catch (Exception)
        {
          maskedToken = "••••••••";
        }
      }

      object? extraConfig = null;
      if (!string.IsNullOrWhiteSpace(p.ExtraConfig))
      {
        try { extraConfig = JsonSerializer.Deserialize<JsonElement>(p.ExtraConfig); }
        catch (JsonException) { /* ignore malformed JSON */ }
      }

      return new
      {
        p.Id,
        providerType = p.ProviderType.ToString().ToLowerInvariant(),
        p.DisplayName,
        p.IsEnabled,
        hasApiToken = !string.IsNullOrWhiteSpace(p.ApiTokenEncrypted),
        token = maskedToken,
        extraConfig,
        p.LastSyncAt,
        p.LastSyncStatus,
        p.LastSyncError,
        p.LastTestAt,
        p.LastTestStatus,
        p.LastTestError
      };
    }).ToList();
  }

  public async Task<bool> UpdateProviderAsync(
    Guid userId, Guid id, UpdateInvestmentProviderRequest request, CancellationToken ct)
  {
    var provider = await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    if (provider is null)
    {
      return false;
    }

    if (request.DisplayName is not null)
    {
      provider.DisplayName = request.DisplayName;
    }

    if (request.ApiToken is not null)
    {
      provider.ApiTokenEncrypted = Protector.Protect(request.ApiToken);
    }

    if (request.ExtraConfig is not null)
    {
      provider.ExtraConfig = request.ExtraConfig.Value.GetRawText();
    }

    provider.UpdatedAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(ct);
    return true;
  }

  public async Task<object?> TestProviderAsync(Guid userId, Guid id, CancellationToken ct)
  {
    var provider = await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    if (provider is null)
    {
      return null;
    }

    try
    {
      if (provider.ProviderType == InvestmentProviderType.Ibkr)
      {
        await ibkrFlexProvider.TestAsync(provider.Id, ct);
      }

      provider.LastTestAt = DateTime.UtcNow;
      provider.LastTestStatus = "success";
      provider.LastTestError = null;
      await dbContext.SaveChangesAsync(ct);

      return new { success = true, message = "Connection test passed." };
    }
    catch (Exception ex)
    {
      provider.LastTestAt = DateTime.UtcNow;
      provider.LastTestStatus = "failure";
      provider.LastTestError = ex.Message;
      await dbContext.SaveChangesAsync(ct);

      logger.LogError(ex, "Provider test failed for {ProviderId}.", id);
      return new { success = false, message = ex.Message };
    }
  }

  public async Task<bool> EnableProviderAsync(Guid userId, Guid id, CancellationToken ct)
  {
    var provider = await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    if (provider is null)
    {
      return false;
    }

    provider.IsEnabled = true;
    provider.UpdatedAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(ct);
    return true;
  }

  public async Task<bool> DisableProviderAsync(Guid userId, Guid id, CancellationToken ct)
  {
    var provider = await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    if (provider is null)
    {
      return false;
    }

    provider.IsEnabled = false;
    provider.UpdatedAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync(ct);
    return true;
  }
}
