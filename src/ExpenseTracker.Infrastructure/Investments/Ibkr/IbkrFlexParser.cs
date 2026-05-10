using System.Globalization;
using System.Xml.Linq;
using ExpenseTracker.Core.Investments;

namespace ExpenseTracker.Infrastructure.Investments.Ibkr;

public sealed class IbkrFlexParser
{
    public IReadOnlyList<PositionData> ParsePositions(string xml)
    {
        var doc = XDocument.Parse(xml);
        var elements = doc.Descendants("OpenPosition");
        return elements.Select(e => new PositionData(
            Symbol: Attr(e, "symbol") ?? "",
            Description: Attr(e, "description") ?? Attr(e, "symbol") ?? "",
            AssetClass: Attr(e, "assetCategory") ?? "OTHER",
            Currency: Attr(e, "currency") ?? "EUR",
            Quantity: DecAttr(e, "position"),
            CostBasisPerShare: DecAttr(e, "costBasisPrice"),
            MarkPrice: DecAttr(e, "markPrice"),
            // Prefer positionValueInBase (already converted to account base currency by IBKR)
            // so USD positions are stored as their EUR equivalent, not face USD value.
            MarketValue: DecAttrPreferBase(e, "positionValueInBase", "positionValue",
                DecAttr(e, "markPrice") * DecAttr(e, "position")),
            UnrealizedPnl: DecAttrPreferBase(e, "fifoPnlUnrealizedInBase", "fifoPnlUnrealized"),
            UnrealizedPnlPercent: ComputePnlPercent(e),
            AccountId: Attr(e, "accountId")
        )).ToList();
    }

    public IReadOnlyList<TradeData> ParseTrades(string xml)
    {
        var doc = XDocument.Parse(xml);
        var elements = doc.Descendants("Trade");
        return elements.Select(e => new TradeData(
            TradeId: Attr(e, "transactionID") ?? Guid.NewGuid().ToString(),
            Symbol: Attr(e, "symbol") ?? "",
            AssetClass: Attr(e, "assetCategory") ?? "OTHER",
            Currency: Attr(e, "currency") ?? "EUR",
            TradeDate: ParseTradeDate(Attr(e, "tradeDate") ?? ""),
            BuySell: Attr(e, "buySell") ?? "BUY",
            Quantity: DecAttr(e, "quantity"),
            Price: DecAttr(e, "tradePrice"),
            Proceeds: DecAttr(e, "proceeds"),
            Commission: DecAttr(e, "ibCommission"),
            NetCash: DecAttr(e, "netCash"),
            AccountId: Attr(e, "accountId")
        )).ToList();
    }

    public IReadOnlyList<CashBalanceData> ParseCashBalances(string xml)
    {
        var doc = XDocument.Parse(xml);
        var elements = doc.Descendants("CashReportCurrency");
        return elements
            .Where(e => Attr(e, "currency") != "BASE_SUMMARY")
            .Select(e => new CashBalanceData(
                Currency: Attr(e, "currency") ?? "EUR",
                Amount: DecAttr(e, "endingCash", DecAttr(e, "endingSettledCash")),
                AccountId: Attr(e, "accountId")
            )).ToList();
    }

    public NavSnapshotData? ParseNav(string xml)
    {
        var doc = XDocument.Parse(xml);
        var element = doc.Descendants("EquitySummaryByReportDateInBase").FirstOrDefault()
            ?? doc.Descendants("EquitySummaryInBase").FirstOrDefault();
        if (element is null) return null;

        var dateStr = Attr(element, "reportDate") ?? Attr(element, "toDate");
        var date = dateStr is not null ? ParseDateOnly(dateStr) : DateOnly.FromDateTime(DateTime.UtcNow);
        var total = DecAttr(element, "total", DecAttr(element, "totalLong") + DecAttr(element, "totalShort"));

        return new NavSnapshotData(date, total, Attr(element, "currency") ?? "EUR", Attr(element, "accountId"));
    }

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    private static decimal DecAttr(XElement e, string name, decimal fallback = 0)
    {
        var val = e.Attribute(name)?.Value;
        return val is not null && decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    /// <summary>
    /// Returns the base-currency attribute value when non-zero, otherwise falls back to the
    /// native-currency attribute, and finally to the provided fallback.
    /// IBKR Flex reports include both e.g. positionValue (USD) and positionValueInBase (EUR).
    /// </summary>
    private static decimal DecAttrPreferBase(XElement e, string baseName, string nativeName, decimal fallback = 0)
    {
        var baseVal = DecAttr(e, baseName);
        if (baseVal != 0) return baseVal;
        return DecAttr(e, nativeName, fallback);
    }

    private static decimal ComputePnlPercent(XElement e)
    {
        var costBasis = DecAttr(e, "costBasisPrice");
        var markPrice = DecAttr(e, "markPrice");
        return costBasis != 0 ? Math.Round((markPrice - costBasis) / costBasis * 100, 4) : 0;
    }

    private static DateTime ParseTradeDate(string dateStr)
    {
        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return DateTime.UtcNow;
    }

    private static DateOnly ParseDateOnly(string dateStr)
    {
        if (DateOnly.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture, out d))
            return d;
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
