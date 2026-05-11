using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IInvestmentService
{
    Task<PortfolioSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct);
    Task<List<AccountSummaryDto>> GetAccountsAsync(Guid userId, CancellationToken ct);
    Task<List<HoldingDto>> GetHoldingsAsync(Guid userId, CancellationToken ct);
    Task<AllocationBreakdownDto> GetAllocationAsync(Guid userId, string type, CancellationToken ct);
    Task<List<HistoryPointDto>> GetHistoryAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<List<RecentActivityDto>> GetActivityAsync(Guid userId, int limit, CancellationToken ct);
    Task<DashboardStripDto> GetDashboardStripAsync(Guid userId, CancellationToken ct);
    Task<object?> GetNarrativeAsync(Guid userId, CancellationToken ct);
    Task<object> TriggerSyncAsync(Guid userId, CancellationToken ct);
    Task<object> GetSyncStatusAsync(Guid userId, CancellationToken ct);
    Task<List<ManualAccountDto>> GetManualAccountsAsync(Guid userId, CancellationToken ct);
    Task<object> CreateManualAccountAsync(Guid userId, CreateManualAccountRequest request, CancellationToken ct);
    Task<bool> UpdateManualAccountAsync(Guid userId, Guid id, UpdateManualAccountRequest request, CancellationToken ct);
    Task<bool> DeleteManualAccountAsync(Guid userId, Guid id, CancellationToken ct);
    Task<object?> UpdateBalanceAsync(Guid userId, Guid id, UpdateBalanceRequest request, CancellationToken ct);
    Task<object?> GetBalanceHistoryAsync(Guid userId, Guid id, CancellationToken ct);
}
