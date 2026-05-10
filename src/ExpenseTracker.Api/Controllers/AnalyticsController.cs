using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(AppDbContext dbContext, ILogger<AnalyticsController> logger) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> GetDashboardAsync(CancellationToken ct)
    {
        try
        {
            var transactions = await LoadTransactionsAsync(ct);
            if (transactions is null) return Unauthorized();

            var now = DateTime.UtcNow;
            var spending = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x)).ToList();
            var last30Start = now.Date.AddDays(-29);
            var prev30Start = last30Start.AddDays(-30);
            var prev30End = last30Start.AddDays(-1);
            var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var currentMonth = SumBetween(spending, currentMonthStart, now);
            var last30 = SumBetween(spending, last30Start, now);
            var prev30 = SumBetween(spending, prev30Start, prev30End);
            var percentChange = prev30 == 0 ? 0 : Math.Round(((last30 - prev30) / prev30) * 100, 2);
            var daysElapsed = Math.Max(1, (now.Date - currentMonthStart.Date).Days + 1);
            var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            var projectedMonthEnd = Math.Round((currentMonth / daysElapsed * daysInMonth) / 10m) * 10m;
            var sameMonthLastYearStart = currentMonthStart.AddYears(-1);
            var sameMonthLastYearEndDay = Math.Min(now.Day, DateTime.DaysInMonth(now.Year - 1, now.Month));
            var sameMonthLastYearEnd = new DateTime(now.Year - 1, now.Month, sameMonthLastYearEndDay, 0, 0, 0, DateTimeKind.Utc)
                .AddDays(1)
                .AddTicks(-1);
            var sameMonthLastYearAmount = SumBetween(spending, sameMonthLastYearStart, sameMonthLastYearEnd);
            decimal? sameMonthLastYear = sameMonthLastYearAmount == 0 ? null : sameMonthLastYearAmount;
            var incomeWidget = BuildIncomeWidget(transactions, now);
            var income30d = incomeWidget.Last30Days;
            var spending30d = last30;
            var netFlow30d = incomeWidget.NetLast30Days;

            var last30Transactions = spending.Where(x => x.TransactionDate >= last30Start && x.TransactionDate <= now).ToList();
            var currentMonthTransactions = spending.Where(x => x.TransactionDate >= currentMonthStart && x.TransactionDate <= now).ToList();
            var previousMonthStart = currentMonthStart.AddMonths(-1);
            var previousMonthEnd = currentMonthStart.AddTicks(-1);
            var previousMonthTransactions = spending.Where(x => x.TransactionDate >= previousMonthStart && x.TransactionDate <= previousMonthEnd).ToList();
            var currentMonthCategoryTotals = currentMonthTransactions
                .GroupBy(GetCategoryKey)
                .ToDictionary(group => group.Key, group => group.Sum(x => x.Amount));
            var previousMonthCategoryTotals = previousMonthTransactions
                .GroupBy(GetCategoryKey)
                .ToDictionary(group => group.Key, group => group.Sum(x => x.Amount));
            var categoryComparisons = currentMonthCategoryTotals.Keys
                .Union(previousMonthCategoryTotals.Keys)
                .Select(categoryName =>
                {
                    var currentAmount = currentMonthCategoryTotals.GetValueOrDefault(categoryName);
                    var previousAmount = previousMonthCategoryTotals.GetValueOrDefault(categoryName);
                    var deltaAmount = currentAmount - previousAmount;
                    var deltaPercent = previousAmount == 0 ? 0 : Math.Round((deltaAmount / previousAmount) * 100, 2);
                    return new CategoryComparison(categoryName, currentAmount, previousAmount, deltaAmount, deltaPercent);
                })
                .OrderByDescending(x => Math.Abs(x.DeltaAmount))
                .ThenByDescending(x => x.CurrentAmount)
                .Take(8)
                .ToList();

            var response = new DashboardResponse(
                new DashboardKpi(currentMonth, last30, prev30, percentChange, projectedMonthEnd, sameMonthLastYear, netFlow30d, income30d, spending30d),
                BuildCategoryBreakdown(last30Transactions),
                categoryComparisons,
                BuildDailySpending(last30Transactions, last30Start.Date, now.Date),
                last30Transactions.Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized))
                    .GroupBy(x => x.MerchantNormalized)
                    .Select(x => new TopMerchant(x.Key, x.Sum(y => y.Amount), x.Count()))
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList(),
                transactions.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).Take(10).Select(x => x.ToDto()).ToList(),
                BuildYtdWidget(spending, now),
                incomeWidget);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch dashboard analytics.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch dashboard analytics.");
        }
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> GetMonthlyAsync([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        try
        {
            var transactions = await LoadTransactionsAsync(ct);
            if (transactions is null) return Unauthorized();

            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);
            var prevStart = start.AddMonths(-1);
            var prevEnd = start.AddTicks(-1);
            var current = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= start && x.TransactionDate <= end).ToList();
            var previous = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= prevStart && x.TransactionDate <= prevEnd).ToList();
            var total = current.Sum(x => x.Amount);
            var previousTotal = previous.Sum(x => x.Amount);
            var percentChange = previousTotal == 0 ? 0 : Math.Round(((total - previousTotal) / previousTotal) * 100, 2);
            var rollingAverage = Enumerable.Range(0, 3)
                .Select(offset => transactions
                    .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= start.AddMonths(-offset) && x.TransactionDate < start.AddMonths(1 - offset))
                    .Sum(x => x.Amount))
                .Average();

            var categoryComparisons = current
                .GroupBy(GetCategoryKey)
                .Select(group =>
                {
                    var currentAmount = group.Sum(x => x.Amount);
                    var previousAmount = previous.Where(x => GetCategoryKey(x) == group.Key).Sum(x => x.Amount);
                    var deltaPercent = previousAmount == 0 ? 0 : Math.Round(((currentAmount - previousAmount) / previousAmount) * 100, 2);
                    return new CategoryComparison(group.Key, currentAmount, previousAmount, currentAmount - previousAmount, deltaPercent);
                })
                .OrderByDescending(x => x.CurrentAmount)
                .ToList();

            return Ok(new MonthlyReportResponse(
                year,
                month,
                total,
                previousTotal,
                percentChange,
                Math.Round(rollingAverage, 2),
                categoryComparisons,
                BuildDailySpending(current, start.Date, end.Date),
                current.Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized))
                    .GroupBy(x => x.MerchantNormalized)
                    .Select(x => new TopMerchant(x.Key, x.Sum(y => y.Amount), x.Count()))
                    .OrderByDescending(x => x.TotalAmount)
                    .Take(10)
                    .ToList()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch monthly analytics for {Year}-{Month}.", year, month);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch monthly analytics.");
        }
    }

    [HttpGet("yearly")]
    public async Task<ActionResult<YearlyReportResponse>> GetYearlyAsync([FromQuery] int year, CancellationToken ct)
    {
        try
        {
            var transactions = await LoadTransactionsAsync(ct);
            if (transactions is null) return Unauthorized();

            var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearEnd = year == DateTime.UtcNow.Year ? DateTime.UtcNow : yearStart.AddYears(1).AddTicks(-1);
            var previousYearStart = yearStart.AddYears(-1);
            var previousYearEnd = yearEnd.AddYears(-1);
            var current = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= yearStart && x.TransactionDate <= yearEnd).ToList();
            var previous = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= previousYearStart && x.TransactionDate <= previousYearEnd).ToList();
            var yearTotal = current.Sum(x => x.Amount);
            var previousYearTotal = previous.Sum(x => x.Amount);
            var percentChange = previousYearTotal == 0 ? 0 : Math.Round(((yearTotal - previousYearTotal) / previousYearTotal) * 100, 2);

            var monthlyCategories = current
                .GroupBy(x => new { x.TransactionDate.Month, CategoryName = GetCategoryKey(x) })
                .Select(x => new MonthlyCategoryTotal(x.Key.Month, x.Key.CategoryName, x.Sum(y => y.Amount)))
                .OrderBy(x => x.Month)
                .ThenBy(x => x.CategoryName)
                .ToList();

            var categoryEvolution = current
                .GroupBy(GetCategoryKey)
                .Select(group => new MonthlyCategorySeries(group.Key, Enumerable.Range(1, 12).Select(month => new MonthlyValue(month, group.Where(x => x.TransactionDate.Month == month).Sum(x => x.Amount))).ToList()))
                .OrderByDescending(x => x.Values.Sum(v => v.Amount))
                .ToList();

            var largestTransactions = current.OrderByDescending(x => x.Amount).Take(20).Select(x => x.ToDto()).ToList();
            return Ok(new YearlyReportResponse(year, yearTotal, previousYearTotal, percentChange, monthlyCategories, largestTransactions, categoryEvolution));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch yearly analytics for {Year}.", year);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch yearly analytics.");
        }
    }

    [HttpGet("dashboard/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetDashboardNarrativeAsync([FromServices] NarrativeService narrativeService, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var result = await narrativeService.GetDashboardNarrativeAsync(userId.Value, ct);
        return Ok(result);
    }

    [HttpGet("monthly/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetMonthlyNarrativeAsync([FromQuery] int year, [FromQuery] int month, [FromServices] NarrativeService narrativeService, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var result = await narrativeService.GetMonthlyNarrativeAsync(userId.Value, year, month, ct);
        return Ok(result);
    }

    [HttpGet("yearly/narrative")]
    public async Task<ActionResult<NarrativeResponse?>> GetYearlyNarrativeAsync([FromQuery] int year, [FromServices] NarrativeService narrativeService, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        var result = await narrativeService.GetYearlyNarrativeAsync(userId.Value, year, ct);
        return Ok(result);
    }

    [HttpPost("regenerate-narratives")]
    public async Task<ActionResult> RegenerateNarrativesAsync([FromServices] NarrativeService narrativeService, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var now = DateTime.UtcNow;
        await narrativeService.RegenerateDashboardNarrativeAsync(userId.Value, ct);
        await narrativeService.RegenerateMonthlyNarrativeAsync(userId.Value, now.Year, now.Month, ct);
        await narrativeService.RegenerateYearlyNarrativeAsync(userId.Value, now.Year, ct);
        return Ok();
    }

    [HttpGet("cost-summary")]
    public async Task<ActionResult> GetCostSummaryAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var logs = await dbContext.LlmCallLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId.Value && log.CreatedAt >= monthStart && log.Success)
            .ToListAsync(ct);

        var categorizationLogs = logs
            .Where(log => string.Equals(log.Purpose, "categorize", StringComparison.Ordinal))
            .ToList();
        var narrativeLogs = logs
            .Where(log => !string.IsNullOrEmpty(log.Purpose) && log.Purpose.StartsWith("summary:", StringComparison.Ordinal))
            .ToList();

        var summaryTokens = await dbContext.Summaries
            .AsNoTracking()
            .Where(summary => summary.UserId == userId.Value && summary.GeneratedAt >= monthStart)
            .SumAsync(summary => summary.TokensUsed ?? 0, ct);

        return Ok(new
        {
            categorization = new
            {
                count = categorizationLogs.Count,
                tokens = 0
            },
            narrative = new
            {
                count = narrativeLogs.Count,
                tokens = summaryTokens
            },
            totalCalls = logs.Count
        });
    }

    [HttpGet("insights")]
    public async Task<ActionResult<InsightsResponse>> GetInsightsAsync(CancellationToken ct)
    {
        try
        {
            var transactions = await LoadTransactionsAsync(ct);
            if (transactions is null) return Unauthorized();

            var now = DateTime.UtcNow;
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var spending = transactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= yearStart).ToList();
            var calendarHeatmap = spending.GroupBy(x => x.TransactionDate.Date).Select(x => new CalendarHeatmapPoint(x.Key, x.Sum(y => y.Amount))).OrderBy(x => x.Date).ToList();
            var dayOfWeekAverages = spending.GroupBy(x => x.TransactionDate.DayOfWeek).Select(x => new DayOfWeekAverage(x.Key.ToString(), Math.Round(x.Average(y => y.Amount), 2), x.Count())).ToList();
            var adHocCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Groceries", "Restaurants", "Fuel", "Shopping", "Food", "Dining"
            };

            var recurringTransactions = spending
                .Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized))
                .GroupBy(x => x.MerchantNormalized)
                .Where(group => group.Count() >= 3)
                .Select(group =>
                {
                    var ordered = group.OrderBy(y => y.TransactionDate).ToList();
                    var average = group.Average(y => y.Amount);
                    var latest = ordered[^1];
                    var count = ordered.Count;
                    var cadence = DetermineCadence(ordered);
                    var categoryName = latest.Category.ParentCategory?.Name ?? latest.Category.Name;
                    var isAdHoc = adHocCategories.Contains(categoryName);
                    var deviationPercent = average > 0 ? Math.Round((double)Math.Abs(latest.Amount - average) / (double)average * 100, 1) : 0;
                    var deviationAbsolute = Math.Abs(latest.Amount - average);
                    var isAnomaly = !isAdHoc
                        && cadence != "variable"
                        && count >= 4
                        && deviationPercent > 25
                        && deviationAbsolute > 5;

                    return new RecurringTransactionInsight(
                        group.Key,
                        Math.Round(average, 2),
                        latest.Amount,
                        latest.TransactionDate,
                        isAnomaly,
                        cadence,
                        count,
                        (decimal)deviationPercent);
                })
                .OrderByDescending(x => x.LatestDate)
                .ToList();

            var subscriptionAnomalies = recurringTransactions
                .Where(x => x.IsAnomaly)
                .OrderByDescending(x => x.DeviationPercent)
                .ToList();

            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var firstTimeMerchants = spending
                .Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized) && x.TransactionDate >= monthStart)
                .GroupBy(x => x.MerchantNormalized)
                .Where(group => transactions.All(t => !string.Equals(t.MerchantNormalized, group.Key, StringComparison.Ordinal) || t.TransactionDate >= monthStart))
                .Select(group =>
                {
                    var first = group.OrderBy(x => x.TransactionDate).First();
                    return new FirstTimeMerchantInsight(group.Key, first.Amount, first.TransactionDate);
                })
                .OrderByDescending(x => x.TransactionDate)
                .ToList();

            var quietDays = Enumerable.Range(0, DateTime.DaysInMonth(now.Year, now.Month))
                .Select(offset => monthStart.AddDays(offset).Date)
                .Where(date => date <= now.Date && spending.All(x => x.TransactionDate.Date != date))
                .ToList();

            return Ok(new InsightsResponse(calendarHeatmap, dayOfWeekAverages, recurringTransactions, subscriptionAnomalies, firstTimeMerchants, quietDays));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch insights analytics.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to fetch insights analytics.");
        }
    }

    private async Task<List<Transaction>?> LoadTransactionsAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return null;

        return await dbContext.Transactions
            .AsNoTracking()
            .Include(x => x.Category)
            .ThenInclude(x => x.ParentCategory)
            .Where(x => x.UserId == userId.Value && !x.IsDeleted)
            .ToListAsync(ct);
    }

    private static decimal SumBetween(IEnumerable<Transaction> transactions, DateTime from, DateTime to)
        => transactions.Where(x => x.TransactionDate >= from && x.TransactionDate <= to).Sum(x => x.Amount);

    private static List<CategoryBreakdown> BuildCategoryBreakdown(IEnumerable<Transaction> transactions)
    {
        var grouped = transactions
            .GroupBy(x => new { Name = GetCategoryKey(x), Color = x.Category.ParentCategory?.Color ?? x.Category.Color })
            .Select(x => new { x.Key.Name, x.Key.Color, Amount = x.Sum(y => y.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();
        var total = grouped.Sum(x => x.Amount);
        return grouped.Select(x => new CategoryBreakdown(x.Name, x.Color, x.Amount, total == 0 ? 0 : Math.Round(x.Amount / total * 100, 2))).ToList();
    }

    private static List<DailySpending> BuildDailySpending(IEnumerable<Transaction> transactions, DateTime start, DateTime end)
    {
        var days = Enumerable.Range(0, Math.Max((end - start).Days + 1, 1)).Select(offset => start.AddDays(offset)).ToList();
        var lookup = transactions.GroupBy(x => x.TransactionDate.Date).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var series = days.Select(day => new { Day = day, Amount = lookup.GetValueOrDefault(day) }).ToList();

        return series.Select((point, index) => new DailySpending(
            point.Day,
            point.Amount,
            Math.Round(series.Skip(Math.Max(0, index - 6)).Take(Math.Min(index + 1, 7)).Average(x => x.Amount), 2),
            Math.Round(series.Skip(Math.Max(0, index - 29)).Take(Math.Min(index + 1, 30)).Average(x => x.Amount), 2)))
            .ToList();
    }

    private static YtdWidget BuildYtdWidget(IEnumerable<Transaction> transactions, DateTime now)
    {
        var start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ytdTransactions = transactions.Where(x => x.TransactionDate >= start && x.TransactionDate <= now).ToList();
        var total = ytdTransactions.Sum(x => x.Amount);
        var daysElapsed = Math.Max((now.Date - start.Date).Days + 1, 1);
        var avgPerDay = Math.Round(total / daysElapsed, 2);
        var projected = Math.Round(avgPerDay * (DateTime.IsLeapYear(now.Year) ? 366 : 365), 2);
        var previousStart = start.AddYears(-1);
        var previousComparableEnd = previousStart.AddDays(daysElapsed - 1);
        var previousComparable = transactions.Where(x => x.TransactionDate >= previousStart && x.TransactionDate <= previousComparableEnd).Sum(x => x.Amount);
        var yoyDelta = previousComparable == 0 ? (decimal?)null : Math.Round(((total - previousComparable) / previousComparable) * 100, 2);
        return new YtdWidget(total, avgPerDay, projected, yoyDelta, BuildCategoryBreakdown(ytdTransactions).Take(5).ToList());
    }

    private static string DetermineCadence(List<Transaction> orderedTransactions)
    {
        if (orderedTransactions.Count < 3) return "variable";

        var intervals = new List<double>();
        for (int i = 1; i < orderedTransactions.Count; i++)
        {
            intervals.Add((orderedTransactions[i].TransactionDate - orderedTransactions[i - 1].TransactionDate).TotalDays);
        }

        var avgInterval = intervals.Average();
        var stdDev = Math.Sqrt(intervals.Average(x => Math.Pow(x - avgInterval, 2)));
        var cv = avgInterval > 0 ? stdDev / avgInterval : double.MaxValue;

        if (cv > 0.5) return "variable";
        if (avgInterval <= 10) return "weekly";
        if (avgInterval <= 45) return "monthly";
        if (avgInterval <= 380) return "yearly";
        return "variable";
    }

    private static string GetCategoryKey(Transaction transaction)
        => transaction.Category.ParentCategory?.Name ?? transaction.Category.Name;

    private static bool IsExcludedFromExpenses(Transaction transaction)
        => transaction.Category.ExcludeFromExpenses || transaction.Category.ParentCategory?.ExcludeFromExpenses == true;

    private static bool IsExcludedFromIncome(Transaction transaction)
        => transaction.Category.ExcludeFromIncome || transaction.Category.ParentCategory?.ExcludeFromIncome == true;

    private static IncomeWidget BuildIncomeWidget(List<Transaction> allTransactions, DateTime now)
    {
        var income = allTransactions.Where(x => x.Direction == Direction.Credit && !IsExcludedFromIncome(x)).ToList();
        var spending = allTransactions.Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x)).ToList();

        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var last30Start = now.Date.AddDays(-29);
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var currentMonthIncome = SumBetween(income, currentMonthStart, now);
        var last30Income = SumBetween(income, last30Start, now);
        var last30Spending = SumBetween(spending, last30Start, now);
        var ytdIncome = SumBetween(income, yearStart, now);

        var sources = income
            .Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized))
            .GroupBy(x => x.MerchantNormalized)
            .Select(g => new IncomeSource(g.Key, g.Sum(x => x.Amount), g.Count()))
            .OrderByDescending(x => x.TotalAmount)
            .Take(10)
            .ToList();

        // Build monthly comparison for the last 6 months
        var months = Enumerable.Range(0, 6)
            .Select(offset => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-offset))
            .Reverse()
            .ToList();

        var monthlyComparison = months.Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
            var monthIncome = SumBetween(income, monthStart, monthEnd);
            var monthSpending = SumBetween(spending, monthStart, monthEnd);
            var label = monthStart.ToString("MMM yyyy");
            return new MonthlyIncomeVsSpending(monthStart.Year, monthStart.Month, label, monthIncome, monthSpending, monthIncome - monthSpending);
        }).ToList();

        return new IncomeWidget(currentMonthIncome, last30Income, ytdIncome, last30Income - last30Spending, sources, monthlyComparison);
    }
}
}

