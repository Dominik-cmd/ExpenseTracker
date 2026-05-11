using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Interfaces;

public interface IRawMessageRepository
{
    Task<RawMessage?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<RawMessage?> GetByIdWithTransactionsAsync(Guid id, CancellationToken ct);
    Task<List<RawMessage>> GetByStatusAsync(Guid userId, ParseStatus? status, CancellationToken ct);
    Task<List<Guid>> GetPendingIdsAsync(CancellationToken ct);
    Task<int> GetPendingCountAsync(Guid userId, CancellationToken ct);
    Task<List<RawMessage>> GetRecentProcessedAsync(Guid userId, int count, CancellationToken ct);
    Task<bool> ExistsByHashAsync(string idempotencyHash, CancellationToken ct);
    Task AddAsync(RawMessage message, CancellationToken ct);
    Task RemoveAsync(RawMessage message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
