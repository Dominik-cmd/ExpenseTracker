using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Core.Investments;

public interface IInvestmentDataProvider
{
    InvestmentProviderType ProviderType { get; }
    string DisplayName { get; }
    bool RequiresPeriodicSync { get; }

    Task<InvestmentSyncResult> SyncAsync(Guid providerId, CancellationToken cancellationToken);
    Task<ProviderTestResult> TestAsync(Guid providerId, CancellationToken cancellationToken);
}

public record InvestmentSyncResult(
    IReadOnlyList<PositionData> Positions,
    IReadOnlyList<TradeData> Trades,
    IReadOnlyList<CashBalanceData> CashBalances,
    NavSnapshotData? NavSnapshot,
    DateTimeOffset SyncedAt,
    string? Warning);

public record PositionData(
    string Symbol,
    string Description,
    string AssetClass,
    string Currency,
    decimal Quantity,
    decimal CostBasisPerShare,
    decimal MarkPrice,
    decimal MarketValue,
    decimal UnrealizedPnl,
    decimal UnrealizedPnlPercent,
    string? AccountId);

public record TradeData(
    string TradeId,
    string Symbol,
    string AssetClass,
    string Currency,
    DateTime TradeDate,
    string BuySell,
    decimal Quantity,
    decimal Price,
    decimal Proceeds,
    decimal Commission,
    decimal NetCash,
    string? AccountId);

public record CashBalanceData(
    string Currency,
    decimal Amount,
    string? AccountId);

public record NavSnapshotData(
    DateOnly Date,
    decimal TotalValue,
    string Currency,
    string? AccountId);

public record ProviderTestResult(bool Success, string Message, TimeSpan Latency);
