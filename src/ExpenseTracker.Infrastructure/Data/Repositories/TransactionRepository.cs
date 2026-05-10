using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class TransactionRepository(AppDbContext dbContext) : ITransactionRepository
{
  public async Task<Transaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.Transactions
      .Include(x => x.Category)
      .ThenInclude(x => x.ParentCategory)
      .Where(x => x.Id == id && x.UserId == userId && !x.IsDeleted)
      .FirstOrDefaultAsync(ct);
  }

  public async Task<(List<Transaction> Items, int TotalCount)> GetPagedAsync(
    Guid userId, TransactionFilter filter, int page, int pageSize, CancellationToken ct)
  {
    var query = BuildQuery(userId, filter);

    var totalCount = await query.CountAsync(ct);

    var items = await query
      .OrderByDescending(x => x.TransactionDate)
      .ThenByDescending(x => x.CreatedAt)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(ct);

    return (items, totalCount);
  }

  public async Task<List<Transaction>> GetAllAsync(
    Guid userId, TransactionFilter filter, CancellationToken ct)
  {
    return await BuildQuery(userId, filter)
      .OrderByDescending(x => x.TransactionDate)
      .ThenByDescending(x => x.CreatedAt)
      .ToListAsync(ct);
  }

  public async Task<List<Transaction>> GetAllForUserAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.Transactions
      .Include(x => x.Category)
      .ThenInclude(x => x.ParentCategory)
      .Where(x => x.UserId == userId && !x.IsDeleted)
      .OrderByDescending(x => x.TransactionDate)
      .ThenByDescending(x => x.CreatedAt)
      .ToListAsync(ct);
  }

  public async Task AddAsync(Transaction transaction, CancellationToken ct)
  {
    await dbContext.Transactions.AddAsync(transaction, ct);
  }

  public async Task<List<Transaction>> GetByCategoryIdsAsync(List<Guid> categoryIds, CancellationToken ct)
  {
    return await dbContext.Transactions
      .Where(x => categoryIds.Contains(x.CategoryId))
      .ToListAsync(ct);
  }

  public async Task<List<Transaction>> GetByIdsAsync(List<Guid> ids, Guid userId, CancellationToken ct)
  {
    return await dbContext.Transactions
      .Include(x => x.Category)
      .ThenInclude(x => x.ParentCategory)
      .Where(x => ids.Contains(x.Id) && x.UserId == userId && !x.IsDeleted)
      .ToListAsync(ct);
  }

  public async Task<List<Guid>> GetUncategorizedRawMessageIdsAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.Transactions
      .Where(t => t.UserId == userId
        && t.CategorySource == Core.Enums.CategorySource.Default
        && t.RawMessageId != null)
      .Select(t => t.RawMessageId!.Value)
      .Distinct()
      .ToListAsync(ct);
  }

  public Task RemoveRangeAsync(IEnumerable<Transaction> transactions, CancellationToken ct)
  {
    dbContext.Transactions.RemoveRange(transactions);
    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }

  private IQueryable<Transaction> BuildQuery(Guid userId, TransactionFilter filter)
  {
    var query = dbContext.Transactions
      .Include(x => x.Category)
      .ThenInclude(x => x.ParentCategory)
      .Where(x => x.UserId == userId && !x.IsDeleted)
      .AsQueryable();

    if (filter.From.HasValue)
    {
      var from = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
      query = query.Where(x => x.TransactionDate >= from);
    }

    if (filter.To.HasValue)
    {
      var to = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);
      query = query.Where(x => x.TransactionDate <= to);
    }

    if (filter.CategoryId.HasValue)
    {
      var catId = filter.CategoryId.Value;
      query = query.Where(x => x.CategoryId == catId || x.Category.ParentCategoryId == catId);
    }

    if (filter.CategoryIds is { Count: > 0 })
    {
      var catIds = filter.CategoryIds;
      query = query.Where(x => catIds.Contains(x.CategoryId) || (x.Category.ParentCategoryId != null && catIds.Contains(x.Category.ParentCategoryId.Value)));
    }

    if (!string.IsNullOrWhiteSpace(filter.Merchant))
    {
      var merchant = filter.Merchant.ToUpper();
      query = query.Where(x => (x.MerchantRaw + " " + x.MerchantNormalized).ToUpper().Contains(merchant));
    }

    if (filter.MinAmount.HasValue)
    {
      query = query.Where(x => x.Amount >= filter.MinAmount.Value);
    }

    if (filter.MaxAmount.HasValue)
    {
      query = query.Where(x => x.Amount <= filter.MaxAmount.Value);
    }

    if (filter.Direction.HasValue)
    {
      query = query.Where(x => x.Direction == filter.Direction.Value);
    }

    if (filter.Source.HasValue)
    {
      query = query.Where(x => x.TransactionSource == filter.Source.Value);
    }

    return query;
  }
}
