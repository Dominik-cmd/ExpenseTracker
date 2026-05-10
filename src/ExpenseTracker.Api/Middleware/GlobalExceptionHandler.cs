using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
  public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
  {
    var (statusCode, title) = exception switch
    {
      InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
      UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden."),
      KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
      _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };

    if (statusCode == StatusCodes.Status500InternalServerError)
    {
      logger.LogError(exception, "Unhandled exception.");
    }

    httpContext.Response.StatusCode = statusCode;
    await httpContext.Response.WriteAsJsonAsync(
      new ProblemDetails
      {
        Status = statusCode,
        Title = title
      },
      cancellationToken);

    return true;
  }
}
