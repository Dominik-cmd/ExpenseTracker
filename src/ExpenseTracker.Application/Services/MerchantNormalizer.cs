namespace ExpenseTracker.Application.Services;

public static class MerchantNormalizer
{
  public static string Normalize(string merchantRaw)
  {
    if (string.IsNullOrWhiteSpace(merchantRaw))
    {
      return string.Empty;
    }

    return merchantRaw.Trim().ToUpperInvariant();
  }
}
