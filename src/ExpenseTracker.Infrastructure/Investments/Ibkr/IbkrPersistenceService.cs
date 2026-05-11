using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Investments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Investments.Ibkr;

public sealed class IbkrPersistenceService(AppDbContext dbContext, ILogger<IbkrPersistenceService> logger)
{
    public async Task PersistAsync(Guid providerId, Guid userId, InvestmentSyncResult result, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var externalAccountIds = result.Positions
                .Select(p => p.AccountId)
                .Concat(result.Trades.Select(t => t.AccountId))
                .Concat(result.CashBalances.Select(c => c.AccountId))
                .Where(id => id is not null)
                .Distinct()
                .ToList();

            var accountMap = new Dictionary<string, Guid>();
            foreach (var externalId in externalAccountIds)
            {
                if (externalId is null) continue;
                var accountId = await EnsureAccountExistsAsync(providerId, userId, externalId, ct);
                accountMap[externalId] = accountId;
            }

            var instrumentKeys = result.Positions.Select(p => (p.Symbol, p.AssetClass, p.Currency))
                .Concat(result.Trades.Select(t => (t.Symbol, t.AssetClass, t.Currency)))
                .Distinct();

            var instrumentMap = new Dictionary<(string, string, string), Guid>();
            foreach (var (symbol, assetClass, currency) in instrumentKeys)
            {
                var instrumentId = await EnsureInstrumentExistsAsync(symbol, assetClass, currency, ct);
                instrumentMap[(symbol, assetClass, currency)] = instrumentId;
            }

            foreach (var externalId in externalAccountIds)
            {
                if (externalId is null || !accountMap.TryGetValue(externalId, out var accountId)) continue;
                var existing = await dbContext.Holdings.Where(h => h.AccountId == accountId).ToListAsync(ct);
                dbContext.Holdings.RemoveRange(existing);
            }
            await dbContext.SaveChangesAsync(ct);

            foreach (var pos in result.Positions)
            {
                if (pos.AccountId is null || !accountMap.TryGetValue(pos.AccountId, out var accountId)) continue;
                var instrumentId = instrumentMap[(pos.Symbol, pos.AssetClass, pos.Currency)];
                dbContext.Holdings.Add(new Holding
                {
                    AccountId = accountId,
                    InstrumentId = instrumentId,
                    Quantity = pos.Quantity,
                    CostBasisPerShare = pos.CostBasisPerShare,
                    MarkPrice = pos.MarkPrice,
                    MarketValue = pos.MarketValue,
                    UnrealizedPnl = pos.UnrealizedPnl,
                    UnrealizedPnlPercent = pos.UnrealizedPnlPercent,
                    Currency = pos.Currency,
                    AsOf = DateTime.UtcNow
                });
            }

            foreach (var trade in result.Trades)
            {
                if (trade.AccountId is null || !accountMap.TryGetValue(trade.AccountId, out var accountId)) continue;
                var instrumentId = instrumentMap.GetValueOrDefault((trade.Symbol, trade.AssetClass, trade.Currency));

                var existing = await dbContext.InvestmentTransactions
                    .FirstOrDefaultAsync(t => t.AccountId == accountId && t.ExternalTransactionId == trade.TradeId, ct);

                if (existing is not null) continue;

                dbContext.InvestmentTransactions.Add(new InvestmentTransaction
                {
                    AccountId = accountId,
                    InstrumentId = instrumentId == Guid.Empty ? null : instrumentId,
                    ExternalTransactionId = trade.TradeId,
                    TransactionType = trade.BuySell,
                    TransactionDate = trade.TradeDate,
                    Quantity = trade.Quantity,
                    Price = trade.Price,
                    GrossAmount = trade.Proceeds,
                    Commission = trade.Commission,
                    NetAmount = trade.NetCash,
                    Currency = trade.Currency,
                    Description = $"{trade.BuySell} {trade.Quantity} {trade.Symbol} @ {trade.Price}"
                });
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("Persisted IBKR sync: {Positions} positions, {Trades} trades",
                result.Positions.Count, result.Trades.Count);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<Guid> EnsureAccountExistsAsync(Guid providerId, Guid userId, string externalId, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
            .FirstOrDefaultAsync(a => a.ProviderId == providerId && a.ExternalAccountId == externalId, ct);

        if (account is not null) return account.Id;

        account = new InvestmentAccount
        {
            ProviderId = providerId,
            UserId = userId,
            ExternalAccountId = externalId,
            DisplayName = externalId,
            AccountType = AccountType.Broker,
            BaseCurrency = "EUR",
            Icon = "trending_up",
            Color = "#2196F3",
            IsActive = true
        };
        dbContext.InvestmentAccounts.Add(account);
        await dbContext.SaveChangesAsync(ct);
        return account.Id;
    }

    private async Task<Guid> EnsureInstrumentExistsAsync(string symbol, string assetClass, string currency, CancellationToken ct)
    {
        var instrument = await dbContext.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol && i.AssetClass == assetClass && i.Currency == currency, ct);

        if (instrument is not null) return instrument.Id;

        instrument = new Instrument
        {
            Symbol = symbol,
            AssetClass = assetClass,
            Currency = currency
        };
        dbContext.Instruments.Add(instrument);
        await dbContext.SaveChangesAsync(ct);
        return instrument.Id;
    }
}
