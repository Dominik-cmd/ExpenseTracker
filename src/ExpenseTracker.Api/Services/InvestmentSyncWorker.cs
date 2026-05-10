using System.Threading.Channels;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Investments;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class InvestmentSyncWorker(
    IServiceProvider serviceProvider,
    ILogger<InvestmentSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InvestmentSyncWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllProvidersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Investment sync failed");
            }

            var nextRun = ComputeNextRunTime();
            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("Next investment sync at {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task SyncAllProvidersAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ibkrProvider = scope.ServiceProvider.GetRequiredService<IbkrFlexProvider>();
        var persistence = scope.ServiceProvider.GetRequiredService<IbkrPersistenceService>();
        var historyService = scope.ServiceProvider.GetRequiredService<PortfolioHistoryService>();

        var enabledProviders = await dbContext.InvestmentProviders
            .Where(p => p.IsEnabled && p.ProviderType == InvestmentProviderType.Ibkr)
            .ToListAsync(ct);

        foreach (var providerConfig in enabledProviders)
        {
            try
            {
                logger.LogInformation("Syncing IBKR provider {ProviderId} for user {UserId}",
                    providerConfig.Id, providerConfig.UserId);

                var result = await ibkrProvider.SyncAsync(providerConfig.Id, ct);
                await persistence.PersistAsync(providerConfig.Id, providerConfig.UserId, result, ct);

                providerConfig.LastSyncAt = DateTime.UtcNow;
                providerConfig.LastSyncStatus = "success";
                providerConfig.LastSyncError = null;
                providerConfig.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);

                logger.LogInformation("IBKR sync completed: {Positions} positions, {Trades} trades",
                    result.Positions.Count, result.Trades.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "IBKR sync failed for provider {ProviderId}", providerConfig.Id);
                providerConfig.LastSyncAt = DateTime.UtcNow;
                providerConfig.LastSyncStatus = "failure";
                providerConfig.LastSyncError = ex.Message;
                providerConfig.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await historyService.SnapshotAllAccountsForDateAsync(today, ct);
    }

    private static DateTimeOffset ComputeNextRunTime()
    {
        var now = DateTimeOffset.UtcNow;
        var todayAt23 = new DateTimeOffset(now.Date.AddHours(23), TimeSpan.Zero);
        return now < todayAt23 ? todayAt23 : todayAt23.AddDays(1);
    }
}
