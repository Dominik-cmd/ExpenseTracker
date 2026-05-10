using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface IMerchantRuleRepository
{
  Task<List<MerchantRule>> GetAllForUserAsync(Guid userId, CancellationToken ct);
  Task<MerchantRule?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
  Task<MerchantRule?> GetByMerchantAsync(Guid userId, string merchantNormalized, CancellationToken ct);
  Task<List<MerchantRule>> GetByCategoryIdsAsync(List<Guid> categoryIds, CancellationToken ct);
  Task AddAsync(MerchantRule rule, CancellationToken ct);
  Task RemoveAsync(MerchantRule rule, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
