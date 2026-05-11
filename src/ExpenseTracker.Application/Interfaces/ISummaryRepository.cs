using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface ISummaryRepository
{
  Task<Summary?> GetLatestAsync(Guid userId, string summaryType, string scope, CancellationToken ct);
  Task<Summary?> GetByCacheKeyAsync(Guid userId, string summaryType, string scope, string cacheKey, CancellationToken ct);
  Task<int> GetTokensUsedForMonthAsync(Guid userId, DateTime monthStart, CancellationToken ct);
  Task RemoveByTypeAndScopeAsync(Guid userId, string summaryType, string scope, CancellationToken ct);
  Task AddAsync(Summary summary, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
