using System.Threading.Channels;

namespace ExpenseTracker.Api.Services;

public sealed class NarrativeRegenerationWorker(
    IServiceProvider serviceProvider,
    Channel<NarrativeRegenerationRequest> channel,
    ILogger<NarrativeRegenerationWorker> logger) : BackgroundService
{
    private readonly Dictionary<string, DateTime> _lastProcessed = new();
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var key = $"{request.UserId}:{request.Type}:{request.Scope}";
                if (_lastProcessed.TryGetValue(key, out var lastProcessedAt)
                    && DateTime.UtcNow - lastProcessedAt < DebounceWindow)
                {
                    continue;
                }

                _lastProcessed[key] = DateTime.UtcNow;

                using var scope = serviceProvider.CreateScope();
                var narrativeService = scope.ServiceProvider.GetRequiredService<NarrativeService>();

                switch (request.Type)
                {
                    case "dashboard":
                        await narrativeService.RegenerateDashboardNarrativeAsync(request.UserId, force: false, stoppingToken);
                        break;
                    case "monthly" when request.Year.HasValue && request.Month.HasValue:
                        await narrativeService.RegenerateMonthlyNarrativeAsync(request.UserId, request.Year.Value, request.Month.Value, force: false, stoppingToken);
                        break;
                    case "yearly" when request.Year.HasValue:
                        await narrativeService.RegenerateYearlyNarrativeAsync(request.UserId, request.Year.Value, force: false, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to regenerate narrative: {Type} {Scope}.", request.Type, request.Scope);
            }
        }
    }
}

public record NarrativeRegenerationRequest(Guid UserId, string Type, string Scope, int? Year = null, int? Month = null);
