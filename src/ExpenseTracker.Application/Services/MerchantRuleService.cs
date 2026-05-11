using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Services;

public sealed class MerchantRuleService(
  IMerchantRuleRepository merchantRuleRepository,
  ITransactionRepository transactionRepository,
  ICategoryRepository categoryRepository) : IMerchantRuleService
{
  public async Task<List<MerchantRuleDto>> GetAllAsync(Guid userId, CancellationToken ct)
  {
    var rules = await merchantRuleRepository.GetAllForUserAsync(userId, ct);
    return rules.Select(r => r.ToDto()).ToList();
  }

  public async Task<MerchantRuleDto?> UpdateAsync(
    Guid id, Guid userId, UpdateMerchantRuleRequest request, CancellationToken ct)
  {
    var rule = await merchantRuleRepository.GetByIdAsync(id, userId, ct);
    if (rule is null)
    {
      return null;
    }

    if (!await categoryRepository.ExistsAsync(request.CategoryId, userId, ct))
    {
      throw new InvalidOperationException("Category does not exist.");
    }

    rule.CategoryId = request.CategoryId;
    rule.UpdatedAt = DateTime.UtcNow;

    if (request.ApplyToExistingTransactions)
    {
      var filter = new TransactionFilter(
        null, null, null, null,
        rule.MerchantNormalized,
        null, null, null, null);

      var transactions = await transactionRepository.GetAllAsync(userId, filter, ct);

      foreach (var transaction in transactions)
      {
        if (string.Equals(transaction.MerchantNormalized, rule.MerchantNormalized, StringComparison.Ordinal))
        {
          transaction.CategoryId = request.CategoryId;
          transaction.CategorySource = CategorySource.Rule;
          transaction.UpdatedAt = DateTime.UtcNow;
        }
      }
    }

    await merchantRuleRepository.SaveChangesAsync(ct);

    rule = await merchantRuleRepository.GetByIdAsync(id, userId, ct);
    return rule?.ToDto();
  }

  public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
  {
    var rule = await merchantRuleRepository.GetByIdAsync(id, userId, ct);
    if (rule is null)
    {
      return false;
    }

    await merchantRuleRepository.RemoveAsync(rule, ct);
    await merchantRuleRepository.SaveChangesAsync(ct);
    return true;
  }
}
