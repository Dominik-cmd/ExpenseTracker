using System.Diagnostics;
using System.Threading.Channels;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Services;

public sealed class LlmProviderService(
  ILlmProviderRepository llmProviderRepository,
  ITransactionRepository transactionRepository,
  IRawMessageRepository rawMessageRepository,
  ILlmProviderResolver providerResolver,
  IDataProtectionProvider dataProtectionProvider,
  Channel<Guid> smsChannel,
  ILogger<LlmProviderService> logger) : ILlmProviderService
{
    private IDataProtector Protector => dataProtectionProvider.CreateProtector("LlmApiKeys.V2");

    public async Task<List<LlmProviderDto>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var providers = await llmProviderRepository.GetAllForUserAsync(userId, ct);
        return providers.Select(p => new LlmProviderDto(
          p.Id, p.ProviderType.ToString(), p.Name, p.Model, p.IsEnabled,
          string.IsNullOrWhiteSpace(p.ApiKeyEncrypted) ? null : "••••••••",
          p.LastTestedAt, p.LastTestStatus?.ToString())).ToList();
    }

    public async Task<LlmProviderDto?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var p = await llmProviderRepository.GetByIdAsync(id, userId, ct);
        if (p is null)
        {
            return null;
        }

        return new LlmProviderDto(
          p.Id, p.ProviderType.ToString(), p.Name, p.Model, p.IsEnabled,
          string.IsNullOrWhiteSpace(p.ApiKeyEncrypted) ? null : "••••••••",
          p.LastTestedAt, p.LastTestStatus?.ToString());
    }

    public async Task<LlmProviderDto?> UpdateAsync(
      Guid userId, Guid id, UpdateLlmProviderRequest request, CancellationToken ct)
    {
        var provider = await llmProviderRepository.GetByIdAsync(id, userId, ct);
        if (provider is null)
        {
            return null;
        }

        if (request.Model is not null)
        {
            provider.Model = request.Model;
        }

        if (request.ApiKey is not null)
        {
            provider.ApiKeyEncrypted = Protector.Protect(request.ApiKey);
        }

        if (request.IsEnabled.HasValue)
        {
            if (request.IsEnabled.Value)
            {
                await llmProviderRepository.DisableAllForUserAsync(userId, ct);
            }

            provider.IsEnabled = request.IsEnabled.Value;
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await llmProviderRepository.SaveChangesAsync(ct);
        providerResolver.InvalidateCache();

        return new LlmProviderDto(
          provider.Id, provider.ProviderType.ToString(), provider.Name,
          provider.Model, provider.IsEnabled,
          string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted) ? null : "••••••••",
          provider.LastTestedAt, provider.LastTestStatus?.ToString());
    }

    public async Task<bool> EnableAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var provider = await llmProviderRepository.GetByIdAsync(id, userId, ct);
        if (provider is null)
        {
            return false;
        }

        await llmProviderRepository.DisableAllForUserAsync(userId, ct);
        provider.IsEnabled = true;
        provider.UpdatedAt = DateTime.UtcNow;
        await llmProviderRepository.SaveChangesAsync(ct);
        providerResolver.InvalidateCache();
        return true;
    }

    public async Task DisableAllAsync(Guid userId, CancellationToken ct)
    {
        await llmProviderRepository.DisableAllForUserAsync(userId, ct);
        await llmProviderRepository.SaveChangesAsync(ct);
        providerResolver.InvalidateCache();
    }

    public async Task<LlmTestResponse?> TestAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var provider = await llmProviderRepository.GetByIdAsync(id, userId, ct);
        if (provider is null)
        {
            return null;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // Temporarily enable only this provider so the resolver picks it up
            var previouslyEnabled = await llmProviderRepository.GetEnabledAsync(userId, ct);
            await llmProviderRepository.DisableAllForUserAsync(userId, ct);
            provider.IsEnabled = true;
            await llmProviderRepository.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();

            var categorizationProvider = await providerResolver.GetActiveProviderAsync(userId, ct);
            if (categorizationProvider is null)
            {
                // Restore previous state
                provider.IsEnabled = false;
                if (previouslyEnabled is not null)
                {
                    var prev = await llmProviderRepository.GetByIdAsync(previouslyEnabled.Id, userId, ct);
                    if (prev is not null) prev.IsEnabled = true;
                }
                await llmProviderRepository.SaveChangesAsync(ct);
                providerResolver.InvalidateCache();
                return new LlmTestResponse(false, sw.Elapsed.TotalMilliseconds, "Provider not configured.");
            }

            // Make a real lightweight test call
            var testCategories = new List<Core.Entities.Category>
      {
        new() { Name = "Test", UserId = userId }
      };
            await categorizationProvider.CategorizeAsync(
              provider,
              new Core.Records.CategorizationRequest("TestMerchant", "TESTMERCHANT", 1.00m,
                Core.Enums.Direction.Debit, Core.Enums.TransactionType.Purchase, "connectivity test")
              { UserId = userId },
              testCategories, ct);

            provider.LastTestedAt = DateTime.UtcNow;
            provider.LastTestStatus = Core.Enums.LlmTestStatus.Success;

            // Restore previous enabled state
            provider.IsEnabled = false;
            if (previouslyEnabled is not null)
            {
                var prev = await llmProviderRepository.GetByIdAsync(previouslyEnabled.Id, userId, ct);
                if (prev is not null) prev.IsEnabled = true;
            }
            else
            {
                // If no provider was previously enabled, keep it disabled
            }
            await llmProviderRepository.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();

            sw.Stop();
            return new LlmTestResponse(true, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            provider.LastTestedAt = DateTime.UtcNow;
            provider.LastTestStatus = Core.Enums.LlmTestStatus.Failed;
            await llmProviderRepository.SaveChangesAsync(ct);
            providerResolver.InvalidateCache();
            logger.LogError(ex, "LLM provider test failed for {ProviderId}.", id);
            return new LlmTestResponse(false, sw.Elapsed.TotalMilliseconds, "Provider test failed. Check API key and model configuration.");
        }
    }

    public async Task<LlmProviderDto?> GetActiveAsync(Guid userId, CancellationToken ct)
    {
        var provider = await llmProviderRepository.GetEnabledAsync(userId, ct);
        if (provider is null)
        {
            return null;
        }

        return new LlmProviderDto(
          provider.Id, provider.ProviderType.ToString(), provider.Name,
          provider.Model, provider.IsEnabled,
          string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted) ? null : "••••••••",
          provider.LastTestedAt, provider.LastTestStatus?.ToString());
    }

    public async Task<RecategorizeUncategorizedResponse> RecategorizeUncategorizedAsync(
      Guid userId, CancellationToken ct)
    {
        var rawMessageIds = await transactionRepository.GetUncategorizedRawMessageIdsAsync(userId, ct);
        if (rawMessageIds.Count == 0)
        {
            return new RecategorizeUncategorizedResponse(0);
        }

        foreach (var id in rawMessageIds)
        {
            var rawMessage = await rawMessageRepository.GetByIdWithTransactionsAsync(id, ct);
            if (rawMessage is null)
            {
                continue;
            }

            await transactionRepository.RemoveRangeAsync(rawMessage.Transactions, ct);
            rawMessage.ParseStatus = Core.Enums.ParseStatus.Pending;
            rawMessage.ErrorMessage = null;
            rawMessage.UpdatedAt = DateTime.UtcNow;
        }

        await transactionRepository.SaveChangesAsync(ct);

        foreach (var id in rawMessageIds)
        {
            await smsChannel.Writer.WriteAsync(id, ct);
        }

        return new RecategorizeUncategorizedResponse(rawMessageIds.Count);
    }
}
