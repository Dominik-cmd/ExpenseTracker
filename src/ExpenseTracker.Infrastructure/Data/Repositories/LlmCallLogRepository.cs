using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class LlmCallLogRepository(AppDbContext dbContext) : ILlmCallLogRepository
{
    public async Task<(List<LlmCallLog> Items, int TotalCount)> GetPagedAsync(
      Guid userId, string? provider, string? purpose, bool? success, int page, int pageSize, CancellationToken ct)
    {
        var query = dbContext.LlmCallLogs
          .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(x => x.ProviderType == provider);
        }

        if (!string.IsNullOrWhiteSpace(purpose))
        {
            query = query.Where(x => x.Purpose == purpose);
        }

        if (success.HasValue)
        {
            query = query.Where(x => x.Success == success.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
          .OrderByDescending(x => x.CreatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .AsNoTracking()
          .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<LlmCallLog>> GetForMonthAsync(Guid userId, DateTime monthStart, CancellationToken ct)
    {
        return await dbContext.LlmCallLogs
          .Where(x => x.UserId == userId && x.CreatedAt >= monthStart && x.Success)
          .AsNoTracking()
          .ToListAsync(ct);
    }

    public async Task AddAsync(LlmCallLog log, CancellationToken ct)
    {
        await dbContext.LlmCallLogs.AddAsync(log, ct);
    }

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var logs = await dbContext.LlmCallLogs
          .Where(x => x.UserId == userId)
          .ToListAsync(ct);

        dbContext.LlmCallLogs.RemoveRange(logs);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
