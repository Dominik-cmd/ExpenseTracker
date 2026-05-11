using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface ILlmCallLogRepository
{
  Task<(List<LlmCallLog> Items, int TotalCount)> GetPagedAsync(Guid userId, string? provider, string? purpose, bool? success, int page, int pageSize, CancellationToken ct);
  Task<List<LlmCallLog>> GetForMonthAsync(Guid userId, DateTime monthStart, CancellationToken ct);
  Task AddAsync(LlmCallLog log, CancellationToken ct);
  Task DeleteAllForUserAsync(Guid userId, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
