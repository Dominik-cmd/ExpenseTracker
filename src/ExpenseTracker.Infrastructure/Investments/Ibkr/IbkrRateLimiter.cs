using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Investments.Ibkr;

/// <summary>
/// Singleton rate limiter enforcing IBKR Flex API limits with a safety margin:
///   - Max 1 request per 1.5 s  (IBKR limit: 1/s)
///   - Max 8 requests per 70 s  (IBKR limit: 10/60 s)
/// </summary>
public sealed class IbkrRateLimiter : IDisposable
{
    private readonly SlidingWindowRateLimiter _perSecond;
    private readonly SlidingWindowRateLimiter _perMinute;
    private readonly ILogger<IbkrRateLimiter> _logger;

    public IbkrRateLimiter(ILogger<IbkrRateLimiter> logger)
    {
        _logger = logger;

        _perSecond = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromSeconds(1.5),
            SegmentsPerWindow = 3,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 20,
        });

        _perMinute = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromSeconds(70),
            SegmentsPerWindow = 7,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 20,
        });
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        // Acquire per-minute slot first (slower, longer queue wait)
        var minuteLease = await _perMinute.AcquireAsync(permitCount: 1, ct);
        if (!minuteLease.IsAcquired)
            throw new InvalidOperationException("IBKR per-minute rate limit queue is full");

        // Then acquire per-second slot to ensure spacing between consecutive calls
        var secondLease = await _perSecond.AcquireAsync(permitCount: 1, ct);
        if (!secondLease.IsAcquired)
        {
            minuteLease.Dispose();
            throw new InvalidOperationException("IBKR per-second rate limit queue is full");
        }

        _logger.LogDebug("IBKR rate limit slots acquired (per-second and per-minute)");

        // Leases are value types / disposable but permit is already counted on acquisition
        minuteLease.Dispose();
        secondLease.Dispose();
    }

    public void Dispose()
    {
        _perSecond.Dispose();
        _perMinute.Dispose();
    }
}
