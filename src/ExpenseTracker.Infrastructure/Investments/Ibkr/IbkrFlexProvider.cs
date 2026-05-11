using System.Diagnostics;
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Investments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Investments.Ibkr;

public sealed class IbkrFlexProvider(
    AppDbContext dbContext,
    IbkrFlexClient client,
    IbkrFlexParser parser,
    IDataProtectionProvider dataProtection,
    ILogger<IbkrFlexProvider> logger) : IInvestmentDataProvider
{
    public InvestmentProviderType ProviderType => InvestmentProviderType.Ibkr;
    public string DisplayName => "Interactive Brokers";
    public bool RequiresPeriodicSync => true;

    public async Task<InvestmentSyncResult> SyncAsync(Guid providerId, CancellationToken ct)
    {
        var config = await dbContext.InvestmentProviders.FindAsync([providerId], ct)
            ?? throw new InvalidOperationException("IBKR provider not found");

        var token = DecryptToken(config);
        var extra = ParseExtraConfig(config.ExtraConfig);

        var positionsXml = await client.RequestAndFetchReportAsync(token, extra.PositionsQueryId, ct);
        var tradesXml = await client.RequestAndFetchReportAsync(token, extra.TradesQueryId, ct);
        var cashXml = await client.RequestAndFetchReportAsync(token, extra.CashQueryId, ct);

        string? navXml = null;
        if (!string.IsNullOrEmpty(extra.NavQueryId))
            navXml = await client.RequestAndFetchReportAsync(token, extra.NavQueryId, ct);

        return new InvestmentSyncResult(
            Positions: parser.ParsePositions(positionsXml),
            Trades: parser.ParseTrades(tradesXml),
            CashBalances: parser.ParseCashBalances(cashXml),
            NavSnapshot: navXml is not null ? parser.ParseNav(navXml) : null,
            SyncedAt: DateTimeOffset.UtcNow,
            Warning: null);
    }

    public async Task<ProviderTestResult> TestAsync(Guid providerId, CancellationToken ct)
    {
        try
        {
            var config = await dbContext.InvestmentProviders.FindAsync([providerId], ct)
                ?? throw new InvalidOperationException("IBKR provider not found");

            var token = DecryptToken(config);
            var extra = ParseExtraConfig(config.ExtraConfig);

            var sw = Stopwatch.StartNew();
            var xml = await client.RequestAndFetchReportAsync(token, extra.PositionsQueryId, ct);
            sw.Stop();

            var positions = parser.ParsePositions(xml);
            return new ProviderTestResult(true, $"Fetched {positions.Count} positions", sw.Elapsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IBKR connection test failed for provider {ProviderId}", providerId);
            return new ProviderTestResult(false, ex.Message, TimeSpan.Zero);
        }
    }

    private string DecryptToken(InvestmentProvider provider)
    {
        if (string.IsNullOrEmpty(provider.ApiTokenEncrypted))
            throw new InvalidOperationException("IBKR API token not configured");

        var protector = dataProtection.CreateProtector("InvestmentApiKeys.V2");
        return protector.Unprotect(provider.ApiTokenEncrypted);
    }

    private static IbkrExtraConfig ParseExtraConfig(string? json)
    {
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("IBKR query configuration not set");

        return JsonSerializer.Deserialize<IbkrExtraConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse IBKR configuration");
    }
}

internal sealed class IbkrExtraConfig
{
    public string PositionsQueryId { get; set; } = "";
    public string TradesQueryId { get; set; } = "";
    public string CashQueryId { get; set; } = "";
    public string? NavQueryId { get; set; }
    public string? TokenExpiresAt { get; set; }
    public string[]? AccountIds { get; set; }
}
