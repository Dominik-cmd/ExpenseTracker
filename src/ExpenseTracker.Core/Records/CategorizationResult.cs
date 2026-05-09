namespace ExpenseTracker.Core.Records;

public sealed record CategorizationResult(
    string CategoryName,
    string? SubcategoryName,
    double Confidence,
    string? Reasoning)
{
    public string Category => CategoryName;
    public string? Subcategory => SubcategoryName;
}
