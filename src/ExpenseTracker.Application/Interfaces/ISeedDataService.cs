namespace ExpenseTracker.Application.Interfaces;

public interface ISeedDataService
{
  Task SeedForUserAsync(Guid userId, CancellationToken ct);
  Task StartAsync(CancellationToken ct);
}
