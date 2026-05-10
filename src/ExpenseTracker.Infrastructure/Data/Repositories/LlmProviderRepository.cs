using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class LlmProviderRepository(AppDbContext dbContext) : ILlmProviderRepository
{
  public async Task<List<LlmProvider>> GetAllForUserAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.LlmProviders
      .Where(x => x.UserId == userId)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<LlmProvider?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.LlmProviders
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<LlmProvider?> GetEnabledAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.LlmProviders
      .FirstOrDefaultAsync(x => x.UserId == userId && x.IsEnabled, ct);
  }

  public async Task DisableAllForUserAsync(Guid userId, CancellationToken ct)
  {
    var providers = await dbContext.LlmProviders
      .Where(x => x.UserId == userId)
      .ToListAsync(ct);

    foreach (var provider in providers)
    {
      provider.IsEnabled = false;
    }
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }
}
