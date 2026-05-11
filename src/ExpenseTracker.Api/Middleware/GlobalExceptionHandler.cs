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
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
              httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (statusCode == StatusCodes.Status400BadRequest)
        {
            logger.LogWarning("Validation error on {Method} {Path}: {Message}",
              httpContext.Request.Method, httpContext.Request.Path, title);

            if (title.Length > 200 || title.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            {
                title = "The request was invalid.";
            }
        }
        else
        {
            logger.LogWarning(exception, "Handled exception ({StatusCode}) on {Method} {Path}",
              statusCode, httpContext.Request.Method, httpContext.Request.Path);
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
