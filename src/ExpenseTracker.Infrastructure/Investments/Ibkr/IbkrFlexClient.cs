using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Investments.Ibkr;

public sealed class IbkrFlexClient(IHttpClientFactory httpClientFactory, ILogger<IbkrFlexClient> logger)
{
    private const string BaseUrl = "https://gdcdyn.interactivebrokers.com/Universal/servlet/FlexStatementService";

    public async Task<string> RequestAndFetchReportAsync(string token, string queryId, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("IbkrFlex");

        var requestUrl = $"{BaseUrl}.SendRequest?t={Uri.EscapeDataString(token)}&q={Uri.EscapeDataString(queryId)}&v=3";
        var requestResponse = await http.GetStringAsync(requestUrl, ct);
        var (referenceCode, retrievalUrl) = ParseSendRequestResponse(requestResponse);

        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fetchUrl = $"{retrievalUrl}?q={Uri.EscapeDataString(referenceCode)}&t={Uri.EscapeDataString(token)}&v=3";
            var response = await http.GetStringAsync(fetchUrl, ct);

            if (IsStillGenerating(response))
            {
                logger.LogDebug("IBKR Flex report still generating, attempt {Attempt}/{Max}", attempt + 1, maxAttempts);
                await Task.Delay(delay, ct);
                continue;
            }

            return response;
        }

        throw new TimeoutException("IBKR Flex Query did not complete within expected time");
    }

    private static (string ReferenceCode, string Url) ParseSendRequestResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root!;

        var status = root.Element("Status")?.Value;
        if (status != "Success")
        {
            var errorCode = root.Element("ErrorCode")?.Value ?? "unknown";
            var errorMessage = root.Element("ErrorMessage")?.Value ?? "Unknown error";
            throw new InvalidOperationException($"IBKR Flex request failed: [{errorCode}] {errorMessage}");
        }

        var referenceCode = root.Element("ReferenceCode")?.Value
            ?? throw new InvalidOperationException("IBKR response missing ReferenceCode");
        var url = root.Element("Url")?.Value
            ?? throw new InvalidOperationException("IBKR response missing Url");

        return (referenceCode, url);
    }

    private static bool IsStillGenerating(string response)
    {
        if (!response.TrimStart().StartsWith("<"))
            return false;

        try
        {
            var doc = XDocument.Parse(response);
            var status = doc.Root?.Element("Status")?.Value;
            var errorCode = doc.Root?.Element("ErrorCode")?.Value;
            return status == "Warn" && errorCode == "1019";
        }
        catch
        {
            return false;
        }
    }
}
