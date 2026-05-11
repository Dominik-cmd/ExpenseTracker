using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface IAuditLogRepository
{
  Task AddAsync(AuditLog log, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
