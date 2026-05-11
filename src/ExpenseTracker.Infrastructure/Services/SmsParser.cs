using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Application.Services;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Records;

namespace ExpenseTracker.Infrastructure;

public sealed class OtpBankaSmsParser
{
    private static readonly Regex TransferOutRegex = new(
        @"^Odliv (\d{2}\.\d{2}\.\d{4});.*?Prejemnik:\s*(.+?);\s*Namen:\s*(.+?);\s*Znesek:\s*([\d,.]+)\s*EUR",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TransferInRegex = new(
        @"^Priliv (\d{2}\.\d{2}\.\d{4});.*?Placnik:\s*(.+?);\s*Namen:\s*(.+?);\s*Znesek:\s*([\d,.]+)\s*EUR",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PurchaseRegex = new(
        @"^(?:POS|SPLET/TEL|SPLET) NAKUP (\d{2}\.\d{2}\.\d{4})\s+(\d{2}:\d{2}),\s*kartica\s*\*{3}\d+,\s*znesek\s*([\d,.]+)\s*EUR,\s*(.+?)(?:,\s*\w+\s+\w{2})?\.?\s*Info:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public ParsedSms? Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var transferOutMatch = TransferOutRegex.Match(body);
        if (transferOutMatch.Success)
        {
            return CreateTransferResult(transferOutMatch, Direction.Debit, TransactionType.TransferOut);
        }

        var transferInMatch = TransferInRegex.Match(body);
        if (transferInMatch.Success)
        {
            return CreateTransferResult(transferInMatch, Direction.Credit, TransactionType.TransferIn);
        }

        var purchaseMatch = PurchaseRegex.Match(body);
        if (!purchaseMatch.Success)
        {
            return null;
        }

        if (!TryParseDateTime(purchaseMatch.Groups[1].Value, purchaseMatch.Groups[2].Value, out var transactionDate) ||
            !TryParseAmount(purchaseMatch.Groups[3].Value, out var amount))
        {
            return null;
        }

        var merchantRaw = purchaseMatch.Groups[4].Value.Trim();
        return new ParsedSms(
            Direction.Debit,
            TransactionType.Purchase,
            amount,
            "EUR",
            transactionDate,
            merchantRaw,
            MerchantNormalizer.Normalize(merchantRaw),
            null);
    }

    private static ParsedSms? CreateTransferResult(Match match, Direction direction, TransactionType defaultType)
    {
        if (!TryParseDateTime(match.Groups[1].Value, null, out var transactionDate) ||
            !TryParseAmount(match.Groups[4].Value, out var amount))
        {
            return null;
        }

        var merchantRaw = match.Groups[2].Value.Trim();
        var notes = match.Groups[3].Value.Trim();
        var transactionType = notes.Contains("DVIG", StringComparison.OrdinalIgnoreCase)
            ? TransactionType.AtmWithdrawal
            : defaultType;

        return new ParsedSms(
            direction,
            transactionType,
            amount,
            "EUR",
            transactionDate,
            merchantRaw,
            MerchantNormalizer.Normalize(merchantRaw),
            notes);
    }

    private static bool TryParseDateTime(string dateValue, string? timeValue, out DateTime result)
    {
        var composite = string.IsNullOrWhiteSpace(timeValue)
            ? dateValue
            : $"{dateValue} {timeValue}";

        var format = string.IsNullOrWhiteSpace(timeValue)
            ? "dd.MM.yyyy"
            : "dd.MM.yyyy HH:mm";

        if (!DateTime.TryParseExact(composite, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            result = default;
            return false;
        }

        result = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return true;
    }

    private static bool TryParseAmount(string rawAmount, out decimal amount)
    {
        var normalized = rawAmount.Trim();
        if (normalized.Contains(',', StringComparison.Ordinal) && normalized.Contains('.', StringComparison.Ordinal))
        {
            normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        normalized = normalized.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
