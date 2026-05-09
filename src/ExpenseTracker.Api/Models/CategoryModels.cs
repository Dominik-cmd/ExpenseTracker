using System;
using System.Collections.Generic;

namespace ExpenseTracker.Api.Models
{


public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Color,
    string? Icon,
    int SortOrder,
    bool IsSystem,
    bool ExcludeFromExpenses,
    Guid? ParentCategoryId,
    List<CategoryDto> SubCategories);

public sealed record CreateCategoryRequest(string Name, string? Color, string? Icon, Guid? ParentCategoryId);

public sealed record UpdateCategoryRequest(string? Name, string? Color, string? Icon, int? SortOrder, bool? ExcludeFromExpenses);

public sealed record DeleteCategoryRequest(Guid ReassignToCategoryId);
}

