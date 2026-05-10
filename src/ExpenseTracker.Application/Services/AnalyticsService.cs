using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.Application.Services;

public sealed class AnalyticsService(
  ITransactionRepository transactionRepository,
  ILlmCallLogRepository llmCallLogRepository,
  ISummaryRepository summaryRepository) : IAnalyticsService
{
  public async Task<DashboardStrip> GetDashboardStripAsync(Guid userId, CancellationToken ct)
  {
    var transactions = await transactionRepository.GetAllForUserAsync(userId, ct);
    var now = DateTime.UtcNow;
    var spending = transactions
      .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x))
      .ToList();
    var income = transactions
      .Where(x => x.Direction == Direction.Credit)
      .ToList();
    var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var monthToDate = SumBetween(spending, currentMonthStart, now);
    var daysElapsed = Math.Max(1, (now.Date - currentMonthStart.Date).Days + 1);
    var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
    var onPace = Math.Round((monthToDate / daysElapsed * daysInMonth) / 10m) * 10m;
    var last30Start = now.Date.AddDays(-29);
    var netLast30Income = SumBetween(income, last30Start, now);
    var netLast30Spending = SumBetween(spending, last30Start, now);
    var netLast30 = netLast30Income - netLast30Spending;

    return new DashboardStrip(monthToDate, onPace, netLast30, netLast30Income, netLast30Spending);
  }

  public async Task<DashboardResponse> GetDashboardAsync(Guid userId, CancellationToken ct)
  {
    var transactions = await transactionRepository.GetAllForUserAsync(userId, ct);
    var now = DateTime.UtcNow;
    var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var currentMonthTransactions = transactions
      .Where(x => x.Direction == Direction.Debit
        && !IsExcludedFromExpenses(x)
        && x.TransactionDate >= currentMonthStart
        && x.TransactionDate <= now)
      .ToList();

    return new DashboardResponse(
      BuildCategoryBreakdown(currentMonthTransactions),
      transactions
        .OrderByDescending(x => x.TransactionDate)
        .ThenByDescending(x => x.CreatedAt)
        .Take(15)
        .Select(x => x.ToDto())
        .ToList(),
      BuildIncomeWidget(transactions, now));
  }

  public async Task<MonthlyReportResponse> GetMonthlyAsync(
    Guid userId, int year, int month, CancellationToken ct)
  {
    var transactions = await transactionRepository.GetAllForUserAsync(userId, ct);
    var now = DateTime.UtcNow;
    var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    var isCurrentMonth = year == now.Year && month == now.Month;
    var end = isCurrentMonth ? now : start.AddMonths(1).AddTicks(-1);
    var prevStart = start.AddMonths(-1);
    var prevEnd = isCurrentMonth
      ? prevStart.AddDays((now.Date - start.Date).Days).AddDays(1).AddTicks(-1)
      : start.AddTicks(-1);
    var fullPrevEnd = start.AddTicks(-1);

    var spending = transactions
      .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x))
      .ToList();
    var current = spending
      .Where(x => x.TransactionDate >= start && x.TransactionDate <= end)
      .ToList();
    var previous = spending
      .Where(x => x.TransactionDate >= prevStart && x.TransactionDate <= prevEnd)
      .ToList();
    var fullPrevious = isCurrentMonth
      ? spending.Where(x => x.TransactionDate >= prevStart && x.TransactionDate <= fullPrevEnd).ToList()
      : previous;

    var total = current.Sum(x => x.Amount);
    var previousTotal = previous.Sum(x => x.Amount);
    var percentChange = previousTotal == 0
      ? 0 : Math.Round(((total - previousTotal) / previousTotal) * 100, 2);
    var rollingAverage = Enumerable.Range(0, 3)
      .Select(offset => spending
        .Where(x => x.TransactionDate >= start.AddMonths(-offset) && x.TransactionDate < start.AddMonths(1 - offset))
        .Sum(x => x.Amount))
      .Average();

    var categoryComparisons = current
      .GroupBy(GetCategoryKey)
      .Select(group =>
      {
        var currentAmount = group.Sum(x => x.Amount);
        var previousAmount = fullPrevious.Where(x => GetCategoryKey(x) == group.Key).Sum(x => x.Amount);
        var deltaPercent = previousAmount == 0
          ? 0 : Math.Round(((currentAmount - previousAmount) / previousAmount) * 100, 2);
        return new CategoryComparison(group.Key, currentAmount, previousAmount, currentAmount - previousAmount, deltaPercent);
      })
      .OrderByDescending(x => x.CurrentAmount)
      .Take(10)
      .ToList();

    return new MonthlyReportResponse(
      year, month, total, previousTotal, percentChange,
      Math.Round(rollingAverage, 2),
      categoryComparisons,
      BuildDailySpending(current, start.Date, end.Date),
      current
        .Where(x => !string.IsNullOrWhiteSpace(x.MerchantNormalized))
        .GroupBy(x => x.MerchantNormalized)
        .Select(x => new TopMerchant(x.Key, x.Sum(y => y.Amount), x.Count()))
        .OrderByDescending(x => x.TotalAmount)
        .Take(10)
        .ToList(),
      BuildDailyCategorySpending(current, start.Date, end.Date));
  }

  public async Task<YearlyReportResponse> GetYearlyAsync(
    Guid userId, int year, CancellationToken ct)
  {
    var transactions = await transactionRepository.GetAllForUserAsync(userId, ct);
    var now = DateTime.UtcNow;
    var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var yearEnd = year == now.Year ? now : yearStart.AddYears(1).AddTicks(-1);
    var previousYearStart = yearStart.AddYears(-1);
    var previousYearEnd = yearEnd.AddYears(-1);

    var spending = transactions
      .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x))
      .ToList();
    var current = spending
      .Where(x => x.TransactionDate >= yearStart && x.TransactionDate <= yearEnd)
      .ToList();
    var previous = spending
      .Where(x => x.TransactionDate >= previousYearStart && x.TransactionDate <= previousYearEnd)
      .ToList();

    var yearTotal = current.Sum(x => x.Amount);
    var previousYearTotal = previous.Sum(x => x.Amount);
    var percentChange = previousYearTotal == 0
      ? 0 : Math.Round(((yearTotal - previousYearTotal) / previousYearTotal) * 100, 2);

    var monthlyCategories = current
      .GroupBy(x => new { x.TransactionDate.Month, CategoryName = GetCategoryKey(x) })
      .Select(x => new MonthlyCategoryTotal(x.Key.Month, x.Key.CategoryName, x.Sum(y => y.Amount)))
      .OrderBy(x => x.Month)
      .ThenBy(x => x.CategoryName)
      .ToList();

    var categoryEvolution = current
      .GroupBy(GetCategoryKey)
      .Select(group => new MonthlyCategorySeries(
        group.Key,
        Enumerable.Range(1, 12)
          .Select(month => new MonthlyValue(month, group.Where(x => x.TransactionDate.Month == month).Sum(x => x.Amount)))
          .ToList()))
      .OrderByDescending(x => x.Values.Sum(v => v.Amount))
      .ToList();

    var largestTransactions = current
      .OrderByDescending(x => x.Amount)
      .Take(20)
      .Select(x => x.ToDto())
      .ToList();

    return new YearlyReportResponse(
      year, yearTotal, previousYearTotal, percentChange,
      monthlyCategories, largestTransactions, categoryEvolution);
  }

  public async Task<InsightsResponse> GetInsightsAsync(Guid userId, CancellationToken ct)
  {
    var transactions = await transactionRepository.GetAllForUserAsync(userId, ct);
    var now = DateTime.UtcNow;
    var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var spending = transactions
      .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x) && x.TransactionDate >= yearStart)
      .ToList();

    var calendarHeatmap = spending
      .GroupBy(x => x.TransactionDate.Date)
      .Select(x => new CalendarHeatmapPoint(x.Key, x.Sum(y => y.Amount)))
      .OrderBy(x => x.Date)
      .ToList();

    var dayOfWeekAverages = spending
      .GroupBy(x => x.TransactionDate.DayOfWeek)
      .Select(x => new DayOfWeekAverage(x.Key.ToString(), Math.Round(x.Average(y => y.Amount), 2), x.Count()))
      .ToList();

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
        var deviationPercent = average > 0
          ? Math.Round((double)Math.Abs(latest.Amount - average) / (double)average * 100, 1)
          : 0;
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
      .Where(group => transactions.All(t =>
        !string.Equals(t.MerchantNormalized, group.Key, StringComparison.Ordinal)
        || t.TransactionDate >= monthStart))
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

    return new InsightsResponse(
      calendarHeatmap, dayOfWeekAverages, recurringTransactions,
      subscriptionAnomalies, firstTimeMerchants, quietDays);
  }

  public async Task<object> GetCostSummaryAsync(Guid userId, CancellationToken ct)
  {
    var now = DateTime.UtcNow;
    var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    var logs = await llmCallLogRepository.GetForMonthAsync(userId, monthStart, ct);
    var categorizationLogs = logs
      .Where(log => string.Equals(log.Purpose, "categorize", StringComparison.Ordinal))
      .ToList();
    var narrativeLogs = logs
      .Where(log => !string.IsNullOrEmpty(log.Purpose) && log.Purpose.StartsWith("summary:", StringComparison.Ordinal))
      .ToList();

    var summaryTokens = await summaryRepository.GetTokensUsedForMonthAsync(userId, monthStart, ct);

    return new
    {
      categorization = new { count = categorizationLogs.Count, tokens = 0 },
      narrative = new { count = narrativeLogs.Count, tokens = summaryTokens },
      totalCalls = logs.Count
    };
  }

  private static decimal SumBetween(IEnumerable<Transaction> transactions, DateTime from, DateTime to)
  {
    return transactions
      .Where(x => x.TransactionDate >= from && x.TransactionDate <= to)
      .Sum(x => x.Amount);
  }

  private static List<CategoryBreakdown> BuildCategoryBreakdown(IEnumerable<Transaction> transactions)
  {
    var grouped = transactions
      .GroupBy(x => new { Name = GetCategoryKey(x), Color = x.Category.ParentCategory?.Color ?? x.Category.Color })
      .Select(x => new { x.Key.Name, x.Key.Color, Amount = x.Sum(y => y.Amount) })
      .OrderByDescending(x => x.Amount)
      .ToList();
    var total = grouped.Sum(x => x.Amount);
    return grouped
      .Select(x => new CategoryBreakdown(x.Name, x.Color, x.Amount, total == 0 ? 0 : Math.Round(x.Amount / total * 100, 2)))
      .ToList();
  }

  private static List<DailySpending> BuildDailySpending(
    IEnumerable<Transaction> transactions, DateTime start, DateTime end)
  {
    var days = Enumerable.Range(0, Math.Max((end - start).Days + 1, 1))
      .Select(offset => start.AddDays(offset))
      .ToList();
    var lookup = transactions
      .GroupBy(x => x.TransactionDate.Date)
      .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
    var series = days
      .Select(day => new { Day = day, Amount = lookup.GetValueOrDefault(day) })
      .ToList();

    return series.Select((point, index) => new DailySpending(
      point.Day,
      point.Amount,
      Math.Round(series.Skip(Math.Max(0, index - 6)).Take(Math.Min(index + 1, 7)).Average(x => x.Amount), 2),
      Math.Round(series.Skip(Math.Max(0, index - 29)).Take(Math.Min(index + 1, 30)).Average(x => x.Amount), 2)))
      .ToList();
  }

  private static List<DailyCategorySpending> BuildDailyCategorySpending(
    List<Transaction> transactions, DateTime start, DateTime end, int topN = 10)
  {
    var topCategories = transactions
      .GroupBy(x => new { Name = GetCategoryKey(x), Color = x.Category.ParentCategory?.Color ?? x.Category.Color })
      .Select(g => new { g.Key.Name, g.Key.Color, Total = g.Sum(x => x.Amount) })
      .OrderByDescending(x => x.Total)
      .Take(topN)
      .ToList();

    var topCategoryNames = topCategories.Select(x => x.Name).ToHashSet();
    var days = Enumerable.Range(0, Math.Max((end - start).Days + 1, 1))
      .Select(offset => start.AddDays(offset).Date)
      .ToList();
    var lookup = transactions
      .Where(x => topCategoryNames.Contains(GetCategoryKey(x)))
      .GroupBy(x => (Date: x.TransactionDate.Date, Category: GetCategoryKey(x)))
      .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

    return topCategories
      .SelectMany(cat => days.Select(day =>
        new DailyCategorySpending(day, cat.Name, cat.Color, lookup.GetValueOrDefault((day, cat.Name)))))
      .ToList();
  }

  private static IncomeWidget BuildIncomeWidget(List<Transaction> allTransactions, DateTime now)
  {
    var income = allTransactions
      .Where(x => x.Direction == Direction.Credit && !IsExcludedFromIncome(x))
      .ToList();
    var spending = allTransactions
      .Where(x => x.Direction == Direction.Debit && !IsExcludedFromExpenses(x))
      .ToList();

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

    var months = Enumerable.Range(0, 6)
      .Select(offset => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-offset))
      .Reverse()
      .ToList();

    var monthlyComparison = months.Select(ms =>
    {
      var monthEnd = ms.AddMonths(1).AddTicks(-1);
      var monthIncome = SumBetween(income, ms, monthEnd);
      var monthSpending = SumBetween(spending, ms, monthEnd);
      var label = ms.ToString("MMM yyyy");
      return new MonthlyIncomeVsSpending(ms.Year, ms.Month, label, monthIncome, monthSpending, monthIncome - monthSpending);
    }).ToList();

    return new IncomeWidget(currentMonthIncome, last30Income, ytdIncome, last30Income - last30Spending, sources, monthlyComparison);
  }

  private static string DetermineCadence(List<Transaction> orderedTransactions)
  {
    if (orderedTransactions.Count < 3)
    {
      return "variable";
    }

    var intervals = new List<double>();
    for (var i = 1; i < orderedTransactions.Count; i++)
    {
      intervals.Add((orderedTransactions[i].TransactionDate - orderedTransactions[i - 1].TransactionDate).TotalDays);
    }

    var avgInterval = intervals.Average();
    var stdDev = Math.Sqrt(intervals.Average(x => Math.Pow(x - avgInterval, 2)));
    var cv = avgInterval > 0 ? stdDev / avgInterval : double.MaxValue;

    if (cv > 0.5)
    {
      return "variable";
    }

    if (avgInterval <= 10)
    {
      return "weekly";
    }

    if (avgInterval <= 45)
    {
      return "monthly";
    }

    if (avgInterval <= 380)
    {
      return "yearly";
    }

    return "variable";
  }

  private static string GetCategoryKey(Transaction transaction)
  {
    return transaction.Category.ParentCategory?.Name ?? transaction.Category.Name;
  }

  private static bool IsExcludedFromExpenses(Transaction transaction)
  {
    return transaction.Category.ExcludeFromExpenses
      || transaction.Category.ParentCategory?.ExcludeFromExpenses == true;
  }

  private static bool IsExcludedFromIncome(Transaction transaction)
  {
    return transaction.Category.ExcludeFromIncome
      || transaction.Category.ParentCategory?.ExcludeFromIncome == true;
  }
}
