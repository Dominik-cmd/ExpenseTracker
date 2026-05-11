using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IAnalyticsService
{
  Task<DashboardStrip> GetDashboardStripAsync(Guid userId, CancellationToken ct);
  Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken ct);
  Task<MonthlyReportResponse> GetMonthlyAsync(Guid userId, int year, int month, CancellationToken ct);
  Task<YearlyReportResponse> GetYearlyAsync(Guid userId, int year, CancellationToken ct);
  Task<InsightsResponse> GetInsightsAsync(Guid userId, CancellationToken ct);
  Task<object> GetCostSummaryAsync(Guid userId, CancellationToken ct);
}
