using System.Text.RegularExpressions;

namespace ExpenseTracker.Application.Services;

public static partial class MerchantNormalizer
{
    private static readonly string[] KnownCities =
    [
        "MARIBOR",
        "LJUBLJANA",
        "CELJE",
        "KOPER",
        "KRANJ",
        "MURSKA SOBOTA",
        "NOVO MESTO",
        "PTUJ"
    ];

    private static readonly Regex CityCountryRegex = new(
        $@"(?:\s+|,\s*)(?:{string.Join("|", KnownCities.Select(Regex.Escape))})\s+[A-Z]{{2}}\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CardDigitsRegex = new(
        @"(?:\s+|,\s*)(?:\*{0,3}\d{4,})\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HexTokenRegex = new(
        @"(?:\s+|,\s*)(?:0X)?[A-F0-9]{6,}\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"(?:\s+|,\s*)(?:\+?\d[\d\s\-/]{5,}\d)\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Normalize(string merchantRaw)
    {
        if (string.IsNullOrWhiteSpace(merchantRaw))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex.Replace(merchantRaw.Trim().ToUpperInvariant(), " ");

        while (true)
        {
            var updated = normalized;
            updated = CityCountryRegex.Replace(updated, string.Empty);
            updated = CardDigitsRegex.Replace(updated, string.Empty);
            updated = HexTokenRegex.Replace(updated, string.Empty);
            updated = PhoneRegex.Replace(updated, string.Empty);
            updated = updated.Trim(' ', ',', '.', ';', '-', '_');

            if (updated == normalized)
            {
                break;
            }

            normalized = updated;
        }

        return WhitespaceRegex.Replace(normalized, " ").Trim();
    }
}
