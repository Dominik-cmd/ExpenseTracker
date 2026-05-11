using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class RawMessageRepository(AppDbContext dbContext) : IRawMessageRepository
{
    public async Task<RawMessage?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        return await dbContext.RawMessages
          .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    }

    public async Task<RawMessage?> GetByIdWithTransactionsAsync(Guid id, CancellationToken ct)
    {
        return await dbContext.RawMessages
          .Include(x => x.Transactions)
          .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<List<RawMessage>> GetByStatusAsync(Guid userId, ParseStatus? status, CancellationToken ct)
    {
        var query = dbContext.RawMessages
          .Include(x => x.Transactions)
          .Where(x => x.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(x => x.ParseStatus == status.Value);
        }

        return await query
          .OrderByDescending(x => x.CreatedAt)
          .AsNoTracking()
          .ToListAsync(ct);
    }

    public async Task<List<Guid>> GetPendingIdsAsync(CancellationToken ct)
    {
        return await dbContext.RawMessages
          .AsNoTracking()
          .Where(x => x.ParseStatus == ParseStatus.Pending)
          .Select(x => x.Id)
          .ToListAsync(ct);
    }

    public async Task<int> GetPendingCountAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.RawMessages
          .CountAsync(x => x.UserId == userId && x.ParseStatus == ParseStatus.Pending, ct);
    }

    public async Task<List<RawMessage>> GetRecentProcessedAsync(Guid userId, int count, CancellationToken ct)
    {
        return await dbContext.RawMessages
          .Include(x => x.Transactions)
          .Where(x => x.UserId == userId && x.ParseStatus != ParseStatus.Pending)
          .OrderByDescending(x => x.UpdatedAt)
          .Take(count)
          .AsNoTracking()
          .ToListAsync(ct);
    }

    public async Task<bool> ExistsByHashAsync(string idempotencyHash, CancellationToken ct)
    {
        return await dbContext.RawMessages
          .AnyAsync(x => x.IdempotencyHash == idempotencyHash, ct);
    }

    public async Task AddAsync(RawMessage message, CancellationToken ct)
    {
        await dbContext.RawMessages.AddAsync(message, ct);
    }

    public Task RemoveAsync(RawMessage message, CancellationToken ct)
    {
        dbContext.RawMessages.Remove(message);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }
}
