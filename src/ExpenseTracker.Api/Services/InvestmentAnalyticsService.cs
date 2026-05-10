using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class InvestmentAnalyticsService(AppDbContext dbContext)
{
    public async Task<PortfolioSummaryDto> GetSummaryAsync(Guid userId, CancellationToken ct)
    {
        var accounts = await dbContext.InvestmentAccounts
            .AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Holdings)
            .Include(a => a.ManualBalance)
            .Where(a => a.UserId == userId && a.IsActive)
            .ToListAsync(ct);

        decimal ibkrValue = 0, manualValue = 0;
        int? oldestManualUpdateDays = null;

        foreach (var account in accounts)
        {
            if (account.Provider.ProviderType == InvestmentProviderType.Ibkr)
            {
                ibkrValue += account.Holdings.Sum(h => h.MarketValue);
                var cashBalances = await dbContext.Holdings
                    .Where(h => h.AccountId == account.Id && h.Instrument.AssetClass == "CASH")
                    .SumAsync(h => h.MarketValue, ct);
                ibkrValue += cashBalances > 0 ? 0 : 0;
            }
            else if (account.Provider.ProviderType == InvestmentProviderType.Manual)
            {
                if (account.ManualBalance is not null)
                {
                    manualValue += account.ManualBalance.Balance;
                    var daysSince = (int)(DateTime.UtcNow - account.ManualBalance.UpdatedAt).TotalDays;
                    if (oldestManualUpdateDays is null || daysSince > oldestManualUpdateDays)
                        oldestManualUpdateDays = daysSince;
                }
            }
        }

        var totalValue = ibkrValue + manualValue;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var todayValue = await dbContext.PortfolioHistories
            .Where(h => h.Account.UserId == userId && h.SnapshotDate == today)
            .SumAsync(h => (decimal?)h.MarketValue, ct) ?? totalValue;
        var yesterdayValue = await dbContext.PortfolioHistories
            .Where(h => h.Account.UserId == userId && h.SnapshotDate == yesterday)
            .SumAsync(h => (decimal?)h.MarketValue, ct);

        decimal? dayChange = yesterdayValue.HasValue ? todayValue - yesterdayValue.Value : null;
        decimal? dayChangePercent = yesterdayValue.HasValue && yesterdayValue.Value != 0
            ? Math.Round((todayValue - yesterdayValue.Value) / yesterdayValue.Value * 100, 2)
            : null;

        var yearStart = new DateOnly(today.Year, 1, 1);
        var yearStartValue = await dbContext.PortfolioHistories
            .Where(h => h.Account.UserId == userId && h.SnapshotDate >= yearStart)
            .OrderBy(h => h.SnapshotDate)
            .Select(h => (decimal?)h.MarketValue)
            .FirstOrDefaultAsync(ct);

        var ytdStartTotal = yearStartValue.HasValue
            ? await dbContext.PortfolioHistories
                .Where(h => h.Account.UserId == userId && h.SnapshotDate == dbContext.PortfolioHistories
                    .Where(h2 => h2.Account.UserId == userId && h2.SnapshotDate >= yearStart)
                    .Min(h2 => h2.SnapshotDate))
                .SumAsync(h => (decimal?)h.MarketValue, ct)
            : null;

        decimal? ytdChange = ytdStartTotal.HasValue ? totalValue - ytdStartTotal.Value : null;
        decimal? ytdChangePercent = ytdStartTotal.HasValue && ytdStartTotal.Value != 0
            ? Math.Round((totalValue - ytdStartTotal.Value) / ytdStartTotal.Value * 100, 2)
            : null;

        return new PortfolioSummaryDto(
            TotalValue: totalValue,
            IbkrValue: ibkrValue,
            ManualValue: manualValue,
            DayChange: dayChange,
            DayChangePercent: dayChangePercent,
            YtdChange: ytdChange,
            YtdChangePercent: ytdChangePercent,
            BaseCurrency: "EUR",
            AsOf: DateTimeOffset.UtcNow,
            OldestManualUpdateDays: oldestManualUpdateDays);
    }

    public async Task<List<AccountSummaryDto>> GetAccountsAsync(Guid userId, CancellationToken ct)
    {
        var accounts = await dbContext.InvestmentAccounts
            .AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Holdings)
            .Include(a => a.ManualBalance)
            .Where(a => a.UserId == userId && a.IsActive)
            .OrderByDescending(a => a.Holdings.Sum(h => h.MarketValue) + (a.ManualBalance != null ? a.ManualBalance.Balance : 0))
            .ToListAsync(ct);

        return accounts.Select(a =>
        {
            var value = a.Provider.ProviderType == InvestmentProviderType.Ibkr
                ? a.Holdings.Sum(h => h.MarketValue)
                : a.ManualBalance?.Balance ?? 0;

            var lastUpdated = a.Provider.ProviderType == InvestmentProviderType.Ibkr
                ? a.Holdings.Any() ? a.Holdings.Max(h => h.AsOf) : (DateTime?)null
                : a.ManualBalance?.UpdatedAt;

            var daysSinceUpdate = lastUpdated.HasValue
                ? (int?)(DateTime.UtcNow - lastUpdated.Value).TotalDays
                : null;

            return new AccountSummaryDto(
                AccountId: a.Id,
                DisplayName: a.DisplayName,
                AccountType: a.AccountType.ToString(),
                ProviderType: a.Provider.ProviderType == InvestmentProviderType.Ibkr ? "ibkr" : "manual",
                Icon: a.Icon ?? DefaultIconForType(a.AccountType),
                Color: a.Color ?? DefaultColorForType(a.AccountType),
                Value: value,
                Currency: a.BaseCurrency,
                ValueInBaseCurrency: value,
                LastUpdated: lastUpdated.HasValue ? new DateTimeOffset(lastUpdated.Value, TimeSpan.Zero) : null,
                DaysSinceUpdate: daysSinceUpdate);
        }).ToList();
    }

    public async Task<List<HoldingDto>> GetHoldingsAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.Holdings
            .AsNoTracking()
            .Include(h => h.Instrument)
            .Include(h => h.Account)
            .Where(h => h.Account.UserId == userId)
            .OrderByDescending(h => h.MarketValue)
            .Select(h => new HoldingDto(
                h.Id,
                h.Account.DisplayName,
                h.Instrument.Symbol,
                h.Instrument.Name,
                h.Instrument.AssetClass,
                h.Quantity,
                h.CostBasisPerShare,
                h.MarkPrice,
                h.MarketValue,
                h.UnrealizedPnl,
                h.UnrealizedPnlPercent,
                h.Currency))
            .ToListAsync(ct);
    }

    public async Task<AllocationBreakdownDto> GetAllocationAsync(Guid userId, string allocationType, CancellationToken ct)
    {
        var accounts = await dbContext.InvestmentAccounts
            .AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Holdings).ThenInclude(h => h.Instrument)
            .Include(a => a.ManualBalance)
            .Where(a => a.UserId == userId && a.IsActive)
            .ToListAsync(ct);

        var items = new List<AllocationItemDto>();

        foreach (var account in accounts)
        {
            if (account.Provider.ProviderType == InvestmentProviderType.Ibkr)
            {
                foreach (var holding in account.Holdings)
                {
                    // Skip CASH positions in currency breakdown — they're residual/operational
                    // cash (e.g. USD dividends) and confuse the allocation picture.
                    if (allocationType.Equals("currency", StringComparison.OrdinalIgnoreCase)
                        && holding.Instrument.AssetClass.Equals("CASH", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var key = allocationType.ToLowerInvariant() switch
                    {
                        "assetclass" => holding.Instrument.AssetClass,
                        "accounttype" => account.AccountType.ToString(),
                        "account" => account.DisplayName,
                        "currency" => holding.Currency,
                        _ => holding.Instrument.AssetClass
                    };
                    items.Add(new AllocationItemDto(key, holding.MarketValue, holding.Currency));
                }
            }
            else if (account.ManualBalance is not null)
            {
                var key = allocationType.ToLowerInvariant() switch
                {
                    "assetclass" => MapAccountTypeToAssetClass(account.AccountType),
                    "accounttype" => account.AccountType.ToString(),
                    "account" => account.DisplayName,
                    "currency" => account.BaseCurrency,
                    _ => MapAccountTypeToAssetClass(account.AccountType)
                };
                items.Add(new AllocationItemDto(key, account.ManualBalance.Balance, account.BaseCurrency));
            }
        }

        var totalValue = items.Sum(i => i.Value);
        var grouped = items
            .GroupBy(i => i.Label)
            .Select(g => new AllocationSliceDto(
                Label: g.Key,
                Value: g.Sum(i => i.Value),
                Percentage: totalValue > 0 ? Math.Round(g.Sum(i => i.Value) / totalValue * 100, 1) : 0))
            .OrderByDescending(s => s.Value)
            .ToList();

        return new AllocationBreakdownDto(allocationType, totalValue, grouped);
    }

    public async Task<List<HistoryPointDto>> GetHistoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var raw = await dbContext.PortfolioHistories
            .AsNoTracking()
            .Where(h => h.Account.UserId == userId && h.SnapshotDate >= from && h.SnapshotDate <= to)
            .Select(h => new { h.SnapshotDate, h.MarketValue })
            .ToListAsync(ct);

        return raw
            .GroupBy(h => h.SnapshotDate)
            .Select(g => new HistoryPointDto(g.Key, g.Sum(h => h.MarketValue)))
            .OrderBy(h => h.Date)
            .ToList();
    }

    public async Task<List<RecentActivityDto>> GetRecentActivityAsync(Guid userId, int limit, CancellationToken ct)
    {
        var ibkrTxns = await dbContext.InvestmentTransactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Instrument)
            .Where(t => t.Account.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(limit)
            .Select(t => new RecentActivityDto(
                new DateTimeOffset(t.TransactionDate, TimeSpan.Zero),
                t.AccountId,
                t.Account.DisplayName,
                "ibkr",
                t.TransactionType,
                t.Description ?? (t.TransactionType + " " + t.Instrument!.Symbol),
                t.NetAmount,
                t.Currency,
                t.Quantity,
                t.Instrument != null ? t.Instrument.Symbol : null))
            .ToListAsync(ct);

        var manualUpdates = await dbContext.ManualBalanceHistories
            .AsNoTracking()
            .Include(h => h.Account)
            .Where(h => h.Account.UserId == userId)
            .OrderByDescending(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new RecentActivityDto(
                new DateTimeOffset(h.RecordedAt, TimeSpan.Zero),
                h.AccountId,
                h.Account.DisplayName,
                "manual",
                "BALANCE_UPDATE",
                "Updated " + h.Account.DisplayName + ": " + h.Currency + h.Balance + (h.Note != null ? " (" + h.Note + ")" : ""),
                h.Balance,
                h.Currency,
                null,
                null))
            .ToListAsync(ct);

        return ibkrTxns.Concat(manualUpdates)
            .OrderByDescending(a => a.Date)
            .Take(limit)
            .ToList();
    }

    public async Task<DashboardStripDto> GetDashboardStripAsync(Guid userId, CancellationToken ct)
    {
        var summary = await GetSummaryAsync(userId, ct);

        return new DashboardStripDto(
            TotalValue: summary.TotalValue,
            DayChange: summary.DayChange,
            DayChangePercent: summary.DayChangePercent,
            YtdChange: summary.YtdChange,
            YtdChangePercent: summary.YtdChangePercent,
            HasData: summary.TotalValue > 0);
    }

    private static string MapAccountTypeToAssetClass(AccountType type) => type switch
    {
        AccountType.Broker => "Stocks",
        AccountType.Savings => "Cash",
        AccountType.Crypto => "Crypto",
        AccountType.Cash => "Cash",
        AccountType.Pension => "Bonds",
        AccountType.RealEstate => "Real Estate",
        _ => "Other"
    };

    public static string DefaultIconForType(AccountType type) => type switch
    {
        AccountType.Broker => "trending_up",
        AccountType.Savings => "savings",
        AccountType.Crypto => "currency_bitcoin",
        AccountType.Cash => "payments",
        AccountType.Pension => "account_balance",
        AccountType.RealEstate => "home",
        _ => "category"
    };

    public static string DefaultColorForType(AccountType type) => type switch
    {
        AccountType.Broker => "#2196F3",
        AccountType.Savings => "#4CAF50",
        AccountType.Crypto => "#FF9800",
        AccountType.Cash => "#9E9E9E",
        AccountType.Pension => "#9C27B0",
        AccountType.RealEstate => "#795548",
        _ => "#607D8B"
    };
}

public record PortfolioSummaryDto(
    decimal TotalValue, decimal IbkrValue, decimal ManualValue,
    decimal? DayChange, decimal? DayChangePercent,
    decimal? YtdChange, decimal? YtdChangePercent,
    string BaseCurrency, DateTimeOffset AsOf, int? OldestManualUpdateDays);

public record AccountSummaryDto(
    Guid AccountId, string DisplayName, string AccountType, string ProviderType,
    string Icon, string Color, decimal Value, string Currency, decimal ValueInBaseCurrency,
    DateTimeOffset? LastUpdated, int? DaysSinceUpdate);

public record HoldingDto(
    Guid Id, string AccountName, string Symbol, string? Name, string AssetClass,
    decimal Quantity, decimal? CostBasisPerShare, decimal? MarkPrice,
    decimal MarketValue, decimal? UnrealizedPnl, decimal? UnrealizedPnlPercent, string Currency);

public record AllocationBreakdownDto(string AllocationType, decimal TotalValue, List<AllocationSliceDto> Slices);
public record AllocationSliceDto(string Label, decimal Value, decimal Percentage);
public record AllocationItemDto(string Label, decimal Value, string Currency);

public record HistoryPointDto(DateOnly Date, decimal Value);

public record RecentActivityDto(
    DateTimeOffset Date, Guid AccountId, string AccountDisplayName, string ProviderType,
    string ActivityType, string Description, decimal? Amount, string Currency,
    decimal? Quantity, string? InstrumentSymbol);

public record DashboardStripDto(
    decimal TotalValue, decimal? DayChange, decimal? DayChangePercent,
    decimal? YtdChange, decimal? YtdChangePercent, bool HasData);
