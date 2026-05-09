namespace ExpenseTracker.Api.Models
{


public sealed record SmsWebhookRequest(string From, string Text, string SentStamp);

public sealed record SmsWebhookResponse(string Status);
}

