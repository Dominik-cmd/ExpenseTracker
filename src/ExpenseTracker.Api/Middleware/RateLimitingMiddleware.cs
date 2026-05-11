namespace ExpenseTracker.Api.Middleware;

public static class RateLimitingMiddleware
{
  public const string LoginPolicyName = "auth-login";
  public const string WebhookPolicyName = "webhook-sms";
}
