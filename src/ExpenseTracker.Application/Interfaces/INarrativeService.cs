using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface INarrativeService
{
    Task<NarrativeResponse?> GetDashboardNarrativeAsync(Guid userId, CancellationToken ct);
    Task<NarrativeResponse?> GetMonthlyNarrativeAsync(Guid userId, int year, int month, CancellationToken ct);
    Task<NarrativeResponse?> GetYearlyNarrativeAsync(Guid userId, int year, CancellationToken ct);
    Task RegenerateAllAsync(Guid userId, CancellationToken ct);
}
