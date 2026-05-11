using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Api.Services;

public sealed class DiagnosticService(OtpBankaSmsParser smsParser) : IDiagnosticService
{
    public DiagnosticParseResponse ParseSms(string text)
    {
        var result = smsParser.Parse(text);
        if (result is null)
        {
            return new DiagnosticParseResponse(false, null, "Could not parse SMS text.");
        }

        return new DiagnosticParseResponse(true, result, null);
    }
}
