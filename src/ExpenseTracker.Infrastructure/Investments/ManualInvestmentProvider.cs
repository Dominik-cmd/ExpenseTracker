using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Investments;

namespace ExpenseTracker.Infrastructure.Investments;

public sealed class ManualInvestmentProvider : IInvestmentDataProvider
{
    public InvestmentProviderType ProviderType => InvestmentProviderType.Manual;
    public string DisplayName => "Manual entries";
    public bool RequiresPeriodicSync => false;

    public Task<InvestmentSyncResult> SyncAsync(Guid providerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new InvestmentSyncResult(
            Positions: Array.Empty<PositionData>(),
            Trades: Array.Empty<TradeData>(),
            CashBalances: Array.Empty<CashBalanceData>(),
            NavSnapshot: null,
            SyncedAt: DateTimeOffset.UtcNow,
            Warning: null));
    }

    public Task<ProviderTestResult> TestAsync(Guid providerId, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ProviderTestResult(true, "Manual provider always available", TimeSpan.Zero));
    }
}
