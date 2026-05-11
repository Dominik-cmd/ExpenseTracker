using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Interfaces;

public interface IInvestmentRepository
{
    Task<InvestmentProvider?> GetProviderAsync(Guid userId, InvestmentProviderType type, CancellationToken ct);
    Task<InvestmentProvider?> GetProviderByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<InvestmentProvider>> GetProvidersForUserAsync(Guid userId, CancellationToken ct);
    Task<InvestmentProvider?> GetEnabledProviderAsync(Guid userId, InvestmentProviderType type, CancellationToken ct);
    Task<List<InvestmentAccount>> GetActiveAccountsAsync(Guid userId, CancellationToken ct);
    Task<List<InvestmentAccount>> GetAccountsByProviderAsync(Guid providerId, Guid userId, CancellationToken ct);
    Task<InvestmentAccount?> GetAccountByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<InvestmentAccount?> GetAccountWithBalanceAsync(Guid id, Guid userId, CancellationToken ct);
    Task AddAccountAsync(InvestmentAccount account, CancellationToken ct);
    Task RemoveAccountAsync(InvestmentAccount account, CancellationToken ct);
    Task AddManualBalanceAsync(ManualAccountBalance balance, CancellationToken ct);
    Task AddBalanceHistoryAsync(ManualBalanceHistory history, CancellationToken ct);
    Task<List<ManualBalanceHistory>> GetBalanceHistoryAsync(Guid accountId, CancellationToken ct);
    Task<List<PortfolioHistory>> GetPortfolioHistoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<PortfolioHistory?> GetPortfolioSnapshotAsync(Guid accountId, DateOnly date, CancellationToken ct);
    Task AddPortfolioHistoryAsync(PortfolioHistory history, CancellationToken ct);
    Task<List<InvestmentTransaction>> GetRecentTransactionsAsync(Guid userId, int limit, CancellationToken ct);
    Task<List<ManualBalanceHistory>> GetRecentBalanceUpdatesAsync(Guid userId, int limit, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
