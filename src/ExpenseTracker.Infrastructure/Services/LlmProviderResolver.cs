using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ExpenseTracker.Infrastructure;

public sealed class LlmProviderResolver : ILlmProviderResolver
{
    public const string ActiveProviderCachePrefix = "llm-provider:active:";

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadOnlyDictionary<LlmProviderType, ILlmCategorizationProvider> _providers;

    public LlmProviderResolver(AppDbContext dbContext, IMemoryCache memoryCache, IEnumerable<ILlmCategorizationProvider> providers)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _providers = providers.ToDictionary(provider => provider.ProviderType);
    }

    public Task<ILlmCategorizationProvider?> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
        => GetActiveProviderAsync(userId, cancellationToken);

    public async Task<ILlmCategorizationProvider?> GetActiveProviderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var enabledProvider = await GetEnabledProviderAsync(userId, cancellationToken);
        if (enabledProvider is null)
        {
            return null;
        }

        return _providers.GetValueOrDefault(enabledProvider.ProviderType);
    }

    public async Task<LlmProvider?> GetEnabledProviderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{ActiveProviderCachePrefix}{userId}";
        if (_memoryCache.TryGetValue(cacheKey, out LlmProvider? provider))
        {
            return provider;
        }

        provider = await _dbContext.LlmProviders
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.IsEnabled, cancellationToken);

        _memoryCache.Set(cacheKey, provider, TimeSpan.FromSeconds(60));
        return provider;
    }

    public void InvalidateCache()
    {
        // Clear all cached providers - simple approach since IMemoryCache doesn't support prefix removal
        // The cache entries will naturally expire after 60 seconds anyway
        if (_memoryCache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }
    }
}
