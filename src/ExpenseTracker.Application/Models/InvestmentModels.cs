namespace ExpenseTracker.Application.Models;

public sealed record PortfolioSummaryDto(
  decimal TotalValue, decimal IbkrValue, decimal ManualValue,
  decimal? DayChange, decimal? DayChangePercent,
  decimal? YtdChange, decimal? YtdChangePercent,
  string BaseCurrency, DateTimeOffset AsOf, int? OldestManualUpdateDays);

public sealed record AccountSummaryDto(
  Guid AccountId, string DisplayName, string AccountType, string ProviderType,
  string Icon, string Color, decimal Value, string Currency, decimal ValueInBaseCurrency,
  DateTimeOffset? LastUpdated, int? DaysSinceUpdate);

public sealed record HoldingDto(
  Guid Id, string AccountName, string Symbol, string? Name, string AssetClass,
  decimal Quantity, decimal? CostBasisPerShare, decimal? MarkPrice,
  decimal MarketValue, decimal? UnrealizedPnl, decimal? UnrealizedPnlPercent, string Currency);

public sealed record AllocationBreakdownDto(string AllocationType, decimal TotalValue, List<AllocationSliceDto> Slices);

public sealed record AllocationSliceDto(string Label, decimal Value, decimal Percentage);

public sealed record HistoryPointDto(DateOnly Date, decimal Value);

public sealed record RecentActivityDto(
  DateTimeOffset Date, Guid AccountId, string AccountDisplayName, string ProviderType,
  string ActivityType, string Description, decimal? Amount, string Currency,
  decimal? Quantity, string? InstrumentSymbol);

public sealed record DashboardStripDto(
  decimal TotalValue, decimal? DayChange, decimal? DayChangePercent,
  decimal? YtdChange, decimal? YtdChangePercent, bool HasData);

public sealed record ManualAccountDto(
  Guid Id, string DisplayName, string AccountType, string Currency,
  decimal? Balance, string? Icon, string? Color, string? Notes,
  bool IsActive, DateTime? LastUpdated);

public sealed record CreateManualAccountRequest(
  string DisplayName, string AccountType, string? Currency,
  decimal? InitialBalance, string? Icon, string? Color, string? Notes);

public sealed record UpdateManualAccountRequest(
  string? DisplayName, string? AccountType, string? Icon,
  string? Color, string? Notes, bool? IsActive);

public sealed record UpdateBalanceRequest(decimal NewBalance, string? Note);

public sealed record UpdateInvestmentProviderRequest(string? DisplayName, string? ApiToken, System.Text.Json.JsonElement? ExtraConfig);
