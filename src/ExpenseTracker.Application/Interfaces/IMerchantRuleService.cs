using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IMerchantRuleService
{
    Task<List<MerchantRuleDto>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<MerchantRuleDto?> UpdateAsync(Guid id, Guid userId, UpdateMerchantRuleRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
