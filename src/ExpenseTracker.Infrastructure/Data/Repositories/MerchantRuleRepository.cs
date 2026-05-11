using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class MerchantRuleRepository(AppDbContext dbContext) : IMerchantRuleRepository
{
    public async Task<List<MerchantRule>> GetAllForUserAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.MerchantRules
          .Include(x => x.Category)
          .ThenInclude(x => x.ParentCategory)
          .Where(x => x.UserId == userId)
          .OrderBy(x => x.MerchantNormalized)
          .AsNoTracking()
          .ToListAsync(ct);
    }

    public async Task<MerchantRule?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        return await dbContext.MerchantRules
          .Include(x => x.Category)
          .ThenInclude(x => x.ParentCategory)
          .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    }

    public async Task<MerchantRule?> GetByMerchantAsync(Guid userId, string merchantNormalized, CancellationToken ct)
    {
        return await dbContext.MerchantRules
          .FirstOrDefaultAsync(x => x.UserId == userId && x.MerchantNormalized == merchantNormalized, ct);
    }

    public async Task<List<MerchantRule>> GetByCategoryIdsAsync(List<Guid> categoryIds, CancellationToken ct)
    {
        return await dbContext.MerchantRules
          .Where(x => categoryIds.Contains(x.CategoryId))
          .ToListAsync(ct);
    }

    public async Task AddAsync(MerchantRule rule, CancellationToken ct)
    {
        await dbContext.MerchantRules.AddAsync(rule, ct);
    }

    public Task RemoveAsync(MerchantRule rule, CancellationToken ct)
    {
        dbContext.MerchantRules.Remove(rule);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
