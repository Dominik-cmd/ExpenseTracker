using System.Diagnostics;

namespace ExpenseTracker.Api.Middleware;

public sealed class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
  public async Task InvokeAsync(HttpContext context)
  {
    var stopwatch = Stopwatch.StartNew();
    var path = context.Request.Path;
    var method = context.Request.Method;

    try
    {
      await next(context);
    }
    finally
    {
      stopwatch.Stop();
      var elapsed = stopwatch.ElapsedMilliseconds;
      var status = context.Response.StatusCode;

      if (elapsed > 500)
      {
        logger.LogWarning("SLOW {Method} {Path} -> {Status} in {Elapsed}ms", method, path, status, elapsed);
      }
      else
      {
        logger.LogInformation("{Method} {Path} -> {Status} in {Elapsed}ms", method, path, status, elapsed);
      }
    }
  }
}
