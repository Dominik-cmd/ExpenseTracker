using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Interfaces;

public interface ITransactionRepository
{
  Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
  Task<(List<Transaction> Items, int TotalCount)> GetPagedAsync(Guid userId, TransactionFilter filter, int page, int pageSize, CancellationToken ct);
  Task<List<Transaction>> GetAllAsync(Guid userId, TransactionFilter filter, CancellationToken ct);
  Task<List<Transaction>> GetAllForUserAsync(Guid userId, CancellationToken ct);
  Task AddAsync(Transaction transaction, CancellationToken ct);
  Task<List<Transaction>> GetByCategoryIdsAsync(List<Guid> categoryIds, CancellationToken ct);
  Task<List<Transaction>> GetByIdsAsync(List<Guid> ids, Guid userId, CancellationToken ct);
  Task<List<Guid>> GetUncategorizedRawMessageIdsAsync(Guid userId, CancellationToken ct);
  Task RemoveRangeAsync(IEnumerable<Transaction> transactions, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}

public sealed record TransactionFilter(
  DateTime? From,
  DateTime? To,
  Guid? CategoryId,
  List<Guid>? CategoryIds,
  string? Merchant,
  decimal? MinAmount,
  decimal? MaxAmount,
  Direction? Direction,
  TransactionSource? Source);
