using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Infrastructure;

public sealed class LlmProviderResolver : ILlmProviderResolver
{
    public const string ActiveProviderCacheKey = "llm-provider:active";

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadOnlyDictionary<LlmProviderType, ILlmCategorizationProvider> _providers;

    public LlmProviderResolver(AppDbContext dbContext, IMemoryCache memoryCache, IEnumerable<ILlmCategorizationProvider> providers)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _providers = providers.ToDictionary(provider => provider.ProviderType);
    }

    public Task<ILlmCategorizationProvider?> ResolveAsync(CancellationToken cancellationToken = default)
        => GetActiveProviderAsync(cancellationToken);

    public async Task<ILlmCategorizationProvider?> GetActiveProviderAsync(CancellationToken cancellationToken = default)
    {
        var enabledProvider = await GetEnabledProviderAsync(cancellationToken);
        if (enabledProvider is null)
        {
            return null;
        }

        return _providers.GetValueOrDefault(enabledProvider.ProviderType);
    }

    public async Task<LlmProvider?> GetEnabledProviderAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(ActiveProviderCacheKey, out LlmProvider? provider))
        {
            return provider;
        }

        provider = await _dbContext.LlmProviders
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsEnabled, cancellationToken);

        _memoryCache.Set(ActiveProviderCacheKey, provider, TimeSpan.FromSeconds(60));
        return provider;
    }

    public void InvalidateCache()
    {
        _memoryCache.Remove(ActiveProviderCacheKey);
    }
}
