using ExpenseTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class DailyNarrativeWorker(
  IServiceProvider serviceProvider,
  ILogger<DailyNarrativeWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      var now = DateTime.UtcNow;
      var nextRun = DateTime.UtcNow.Date.AddDays(now.Hour >= 6 ? 1 : 0).AddHours(6);
      var delay = nextRun - now;

      try
      {
        await Task.Delay(delay, stoppingToken);
      }
      catch (OperationCanceledException)
      {
        return;
      }

      await RunForAllUsersAsync(stoppingToken);
    }
  }

  private async Task RunForAllUsersAsync(CancellationToken ct)
  {
    try
    {
      using var scope = serviceProvider.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var narrativeService = scope.ServiceProvider.GetRequiredService<NarrativeService>();

      var now = DateTime.UtcNow;
      var userIds = await dbContext.Transactions
        .AsNoTracking()
        .Where(t => !t.IsDeleted)
        .Select(t => t.UserId)
        .Distinct()
        .ToListAsync(ct);

      foreach (var userId in userIds)
      {
        try
        {
          await narrativeService.RegenerateDashboardNarrativeAsync(userId, force: false, ct);
          await narrativeService.RegenerateMonthlyNarrativeAsync(userId, now.Year, now.Month, force: false, ct);
          await narrativeService.RegenerateYearlyNarrativeAsync(userId, now.Year, force: false, ct);
        }
        catch (Exception ex)
        {
          logger.LogError(ex, "Failed daily narrative regeneration for user {UserId}.", userId);
        }
      }
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed daily narrative worker run.");
    }
  }
}
