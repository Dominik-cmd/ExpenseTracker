using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct)
    {
        await dbContext.AuditLogs.AddAsync(log, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
