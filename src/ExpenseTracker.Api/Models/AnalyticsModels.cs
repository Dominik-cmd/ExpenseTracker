using System;
using System.Collections.Generic;

namespace ExpenseTracker.Api.Models
{


public sealed record DashboardStrip(
    decimal MonthToDate,
    decimal OnPace,
    decimal NetLast30,
    decimal NetLast30Income,
    decimal NetLast30Spending);

public sealed record DashboardResponse(
    List<CategoryBreakdown> CategoryLeaderboard,
    List<TransactionDto> RecentTransactions,
    IncomeWidget? Income);

public sealed record DashboardKpi(
    decimal CurrentMonth,
    decimal Last30Days,
    decimal Prev30Days,
    decimal PercentChange,
    decimal ProjectedMonthEnd,
    decimal? SameMonthLastYear,
    decimal NetFlow30d,
    decimal Income30d,
    decimal Spending30d);

public sealed record CategoryBreakdown(string Name, string? Color, decimal Amount, decimal Percentage);

public sealed record DailySpending(DateTime Date, decimal Amount, decimal? Rolling7Day, decimal? Rolling30Day);

public sealed record TopMerchant(string Name, decimal TotalAmount, int TransactionCount);

public sealed record YtdWidget(decimal Total, decimal AvgPerDay, decimal ProjectedEoy, decimal? YoyDelta, List<CategoryBreakdown> TopCategories);

public sealed record MonthlyReportResponse(
    int Year,
    int Month,
    decimal TotalAmount,
    decimal PreviousMonthTotal,
    decimal PercentChange,
    decimal Rolling3MonthAverage,
    List<CategoryComparison> CategoryComparisons,
    List<DailySpending> DailySpending,
    List<TopMerchant> TopMerchants,
    List<DailyCategorySpending> DailyCategoryBreakdown);

public sealed record CategoryComparison(string Name, decimal CurrentAmount, decimal PreviousAmount, decimal DeltaAmount, decimal DeltaPercent);

public sealed record DailyCategorySpending(DateTime Date, string CategoryName, string? Color, decimal Amount);

public sealed record YearlyReportResponse(
    int Year,
    decimal YearTotal,
    decimal PreviousYearTotal,
    decimal PercentChange,
    List<MonthlyCategoryTotal> MonthlyCategories,
    List<TransactionDto> LargestTransactions,
    List<MonthlyCategorySeries> CategoryEvolution);

public sealed record MonthlyCategoryTotal(int Month, string CategoryName, decimal Amount);

public sealed record MonthlyCategorySeries(string CategoryName, List<MonthlyValue> Values);

public sealed record MonthlyValue(int Month, decimal Amount);

public sealed record InsightsResponse(
    List<CalendarHeatmapPoint> CalendarHeatmap,
    List<DayOfWeekAverage> DayOfWeekAverages,
    List<RecurringTransactionInsight> RecurringTransactions,
    List<RecurringTransactionInsight> SubscriptionAnomalies,
    List<FirstTimeMerchantInsight> FirstTimeMerchants,
    List<DateTime> QuietDays);

public sealed record CalendarHeatmapPoint(DateTime Date, decimal Amount);

public sealed record DayOfWeekAverage(string DayOfWeek, decimal AverageAmount, int TransactionCount);

public sealed record RecurringTransactionInsight(
    string Merchant,
    decimal AverageAmount,
    decimal LatestAmount,
    DateTime LatestDate,
    bool IsAnomaly,
    string Cadence,
    int OccurrenceCount,
    decimal DeviationPercent);

public sealed record FirstTimeMerchantInsight(string Merchant, decimal Amount, DateTime TransactionDate);

public sealed record IncomeWidget(
    decimal CurrentMonth,
    decimal Last30Days,
    decimal YtdTotal,
    decimal NetLast30Days,
    List<IncomeSource> Sources,
    List<MonthlyIncomeVsSpending> MonthlyComparison);

public sealed record IncomeSource(string Name, decimal TotalAmount, int TransactionCount);

public sealed record MonthlyIncomeVsSpending(int Year, int Month, string Label, decimal Income, decimal Spending, decimal Net);
}

