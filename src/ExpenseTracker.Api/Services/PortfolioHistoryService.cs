using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class PortfolioHistoryService(AppDbContext dbContext, ILogger<PortfolioHistoryService> logger)
{
    public async Task SnapshotAllAccountsForDateAsync(DateOnly date, CancellationToken ct)
    {
        var accounts = await dbContext.InvestmentAccounts
            .Include(a => a.Provider)
            .Include(a => a.Holdings)
            .Include(a => a.ManualBalance)
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        foreach (var account in accounts)
        {
            decimal value;
            string source;

            if (account.Provider.ProviderType == InvestmentProviderType.Ibkr)
            {
                value = account.Holdings.Sum(h => h.MarketValue);
                source = "sync";
            }
            else
            {
                value = account.ManualBalance?.Balance ?? 0;
                source = "manual";
            }

            if (value == 0) continue;

            var existing = await dbContext.PortfolioHistories
                .FirstOrDefaultAsync(h => h.AccountId == account.Id && h.SnapshotDate == date, ct);

            if (existing is not null)
            {
                existing.MarketValue = value;
                existing.Source = source;
            }
            else
            {
                dbContext.PortfolioHistories.Add(new PortfolioHistory
                {
                    AccountId = account.Id,
                    SnapshotDate = date,
                    MarketValue = value,
                    Currency = account.BaseCurrency,
                    Source = source
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Snapshot portfolio history for {Date}: {Count} accounts", date, accounts.Count);
    }

    public async Task SnapshotAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await dbContext.InvestmentAccounts
            .Include(a => a.Provider)
            .Include(a => a.ManualBalance)
            .Include(a => a.Holdings)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (account is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        decimal value;
        string source;

        if (account.Provider.ProviderType == InvestmentProviderType.Ibkr)
        {
            value = account.Holdings.Sum(h => h.MarketValue);
            source = "sync";
        }
        else
        {
            value = account.ManualBalance?.Balance ?? 0;
            source = "manual";
        }

        var existing = await dbContext.PortfolioHistories
            .FirstOrDefaultAsync(h => h.AccountId == accountId && h.SnapshotDate == today, ct);

        if (existing is not null)
        {
            existing.MarketValue = value;
            existing.Source = source;
        }
        else
        {
            dbContext.PortfolioHistories.Add(new PortfolioHistory
            {
                AccountId = accountId,
                SnapshotDate = today,
                MarketValue = value,
                Currency = account.BaseCurrency,
                Source = source
            });
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
