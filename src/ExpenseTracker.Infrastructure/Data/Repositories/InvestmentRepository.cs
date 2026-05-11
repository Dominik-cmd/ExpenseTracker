using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class InvestmentRepository(AppDbContext dbContext) : IInvestmentRepository
{
  public async Task<InvestmentProvider?> GetProviderAsync(Guid userId, InvestmentProviderType type, CancellationToken ct)
  {
    return await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(x => x.UserId == userId && x.ProviderType == type, ct);
  }

  public async Task<InvestmentProvider?> GetProviderByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<List<InvestmentProvider>> GetProvidersForUserAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentProviders
      .Where(x => x.UserId == userId)
      .Include(x => x.Accounts)
      .ToListAsync(ct);
  }

  public async Task<InvestmentProvider?> GetEnabledProviderAsync(
    Guid userId, InvestmentProviderType type, CancellationToken ct)
  {
    return await dbContext.InvestmentProviders
      .FirstOrDefaultAsync(x => x.UserId == userId && x.ProviderType == type && x.IsEnabled, ct);
  }

  public async Task<List<InvestmentAccount>> GetActiveAccountsAsync(Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentAccounts
      .Where(x => x.UserId == userId && x.IsActive)
      .Include(x => x.Provider)
      .Include(x => x.Holdings)
      .Include(x => x.ManualBalance)
      .OrderByDescending(x => x.ManualBalance != null ? x.ManualBalance.Balance : 0)
      .ToListAsync(ct);
  }

  public async Task<List<InvestmentAccount>> GetAccountsByProviderAsync(
    Guid providerId, Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentAccounts
      .Where(x => x.ProviderId == providerId && x.UserId == userId)
      .Include(x => x.ManualBalance)
      .OrderBy(x => x.SortOrder)
      .ThenBy(x => x.DisplayName)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<InvestmentAccount?> GetAccountByIdAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentAccounts
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task<InvestmentAccount?> GetAccountWithBalanceAsync(Guid id, Guid userId, CancellationToken ct)
  {
    return await dbContext.InvestmentAccounts
      .Include(x => x.ManualBalance)
      .Include(x => x.Holdings)
      .Include(x => x.Provider)
      .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
  }

  public async Task AddAccountAsync(InvestmentAccount account, CancellationToken ct)
  {
    await dbContext.InvestmentAccounts.AddAsync(account, ct);
  }

  public Task RemoveAccountAsync(InvestmentAccount account, CancellationToken ct)
  {
    dbContext.InvestmentAccounts.Remove(account);
    return Task.CompletedTask;
  }

  public async Task AddManualBalanceAsync(ManualAccountBalance balance, CancellationToken ct)
  {
    await dbContext.ManualAccountBalances.AddAsync(balance, ct);
  }

  public async Task AddBalanceHistoryAsync(ManualBalanceHistory history, CancellationToken ct)
  {
    await dbContext.ManualBalanceHistories.AddAsync(history, ct);
  }

  public async Task<List<ManualBalanceHistory>> GetBalanceHistoryAsync(Guid accountId, CancellationToken ct)
  {
    return await dbContext.ManualBalanceHistories
      .Where(x => x.AccountId == accountId)
      .OrderByDescending(x => x.RecordedAt)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<List<PortfolioHistory>> GetPortfolioHistoryAsync(
    Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
  {
    return await dbContext.PortfolioHistories
      .Where(x => x.Account.UserId == userId && x.SnapshotDate >= from && x.SnapshotDate <= to)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<PortfolioHistory?> GetPortfolioSnapshotAsync(Guid accountId, DateOnly date, CancellationToken ct)
  {
    return await dbContext.PortfolioHistories
      .FirstOrDefaultAsync(x => x.AccountId == accountId && x.SnapshotDate == date, ct);
  }

  public async Task AddPortfolioHistoryAsync(PortfolioHistory history, CancellationToken ct)
  {
    await dbContext.PortfolioHistories.AddAsync(history, ct);
  }

  public async Task<List<InvestmentTransaction>> GetRecentTransactionsAsync(
    Guid userId, int limit, CancellationToken ct)
  {
    return await dbContext.InvestmentTransactions
      .Include(x => x.Account)
      .Include(x => x.Instrument)
      .Where(x => x.Account.UserId == userId)
      .OrderByDescending(x => x.TransactionDate)
      .Take(limit)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task<List<ManualBalanceHistory>> GetRecentBalanceUpdatesAsync(
    Guid userId, int limit, CancellationToken ct)
  {
    return await dbContext.ManualBalanceHistories
      .Include(x => x.Account)
      .Where(x => x.Account.UserId == userId)
      .OrderByDescending(x => x.RecordedAt)
      .Take(limit)
      .AsNoTracking()
      .ToListAsync(ct);
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }
}
