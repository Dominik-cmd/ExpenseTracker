namespace ExpenseTracker.Core.Records;

public sealed record CategoryHierarchy(string Name, IReadOnlyCollection<string> Subcategories);
