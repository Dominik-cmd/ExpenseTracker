using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class SummaryRepository(AppDbContext dbContext) : ISummaryRepository
{
  public async Task<Summary?> GetLatestAsync(Guid userId, string summaryType, string scope, CancellationToken ct)
  {
    return await dbContext.Summaries
      .Where(x => x.UserId == userId && x.SummaryType == summaryType && x.Scope == scope)
      .OrderByDescending(x => x.GeneratedAt)
      .AsNoTracking()
      .FirstOrDefaultAsync(ct);
  }

  public async Task<Summary?> GetByCacheKeyAsync(
    Guid userId, string summaryType, string scope, string cacheKey, CancellationToken ct)
  {
    return await dbContext.Summaries
      .Where(x => x.UserId == userId && x.SummaryType == summaryType && x.Scope == scope && x.CacheKey == cacheKey)
      .AsNoTracking()
      .FirstOrDefaultAsync(ct);
  }

  public async Task<int> GetTokensUsedForMonthAsync(Guid userId, DateTime monthStart, CancellationToken ct)
  {
    return await dbContext.Summaries
      .Where(x => x.UserId == userId && x.GeneratedAt >= monthStart)
      .SumAsync(x => x.TokensUsed ?? 0, ct);
  }

  public async Task RemoveByTypeAndScopeAsync(Guid userId, string summaryType, string scope, CancellationToken ct)
  {
    var summaries = await dbContext.Summaries
      .Where(x => x.UserId == userId && x.SummaryType == summaryType && x.Scope == scope)
      .ToListAsync(ct);

    dbContext.Summaries.RemoveRange(summaries);
  }

  public async Task AddAsync(Summary summary, CancellationToken ct)
  {
    await dbContext.Summaries.AddAsync(summary, ct);
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }
}
