using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class NarrativeService(
  AppDbContext dbContext,
  ILlmProviderResolver providerResolver,
  ILogger<NarrativeService> logger) : Application.Interfaces.INarrativeService
{
    private const string SystemPrompt = """
    You are an analyst writing concise interpretations for a personal expense tracker.

    Style:
    - Plain English, conversational but informative
    - 1 to 3 sentences depending on what the data warrants
    - Reference specific numbers from the input (no hallucinated figures)
    - Highlight the most useful insight first
    - If nothing is notable, say so directly: "This month is tracking normally."
    - No flattery, padding, hedging, or AI-sounding boilerplate
    - No headers, bullets, or markdown — output is plain prose
    - Use € for euros, no decimals on rounded figures, two decimals on precise figures
    - Slovenian and English merchant names are both common; treat them equivalently

    Never:
    - Compute or estimate numbers not provided in the input
    - Add suggestions or financial advice
    - Speculate about reasons for spending changes unless the input includes context

    Output is one sentence only. Stop after the first period.
    """;

    public async Task<NarrativeResponse?> GetDashboardNarrativeAsync(Guid userId, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("dashboard", "current", userId, ct);
        return cached is null
          ? null
          : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task<NarrativeResponse?> GetMonthlyNarrativeAsync(
      Guid userId, int year, int month, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("monthly", $"{year}-{month:D2}", userId, ct);
        return cached is null
          ? null
          : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task<NarrativeResponse?> GetYearlyNarrativeAsync(
      Guid userId, int year, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("yearly", year.ToString(), userId, ct);
        return cached is null
          ? null
          : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task RegenerateDashboardNarrativeAsync(Guid userId, bool force, CancellationToken ct)
    {
        var input = await BuildDashboardInputAsync(userId, ct);
        if (input is null)
        {
            return;
        }

        await GenerateAndStoreAsync(userId, "dashboard", "current", input, BuildDashboardPrompt(input), force, ct);
    }

    public async Task RegenerateMonthlyNarrativeAsync(
      Guid userId, int year, int month, bool force, CancellationToken ct)
    {
        var input = await BuildMonthlyInputAsync(userId, year, month, ct);
        if (input is null)
        {
            return;
        }

        await GenerateAndStoreAsync(
          userId, "monthly", $"{year}-{month:D2}", input, BuildMonthlyPrompt(input, year, month), force, ct);
    }

    public async Task RegenerateYearlyNarrativeAsync(Guid userId, int year, bool force, CancellationToken ct)
    {
        var input = await BuildYearlyInputAsync(userId, year, ct);
        if (input is null)
        {
            return;
        }

        await GenerateAndStoreAsync(userId, "yearly", year.ToString(), input, BuildYearlyPrompt(input, year), force, ct);
    }

    public async Task RegenerateAllAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await RegenerateDashboardNarrativeAsync(userId, force: true, ct);
        await RegenerateMonthlyNarrativeAsync(userId, now.Year, now.Month, force: true, ct);
        await RegenerateYearlyNarrativeAsync(userId, now.Year, force: true, ct);
        await RegenerateInvestmentNarrativeAsync(userId, force: true, ct);
    }

    public async Task<NarrativeResponse?> GetInvestmentNarrativeAsync(Guid userId, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("investments", "current", userId, ct);
        return cached is null
          ? null
          : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task RegenerateInvestmentNarrativeAsync(Guid userId, bool force, CancellationToken ct)
    {
        var analyticsService = new InvestmentAnalyticsService(dbContext);
        var input = await BuildInvestmentInputAsync(analyticsService, userId, ct);
        if (input is null)
        {
            return;
        }

        await GenerateAndStoreAsync(userId, "investments", "current", input, BuildInvestmentPrompt(input), force, ct);
    }

    private async Task GenerateAndStoreAsync(
      Guid userId, string summaryType, string scope,
      object input, string userPrompt, bool force, CancellationToken ct)
    {
        var provider = await providerResolver.GetNarrativeProviderAsync(userId, ct);
        var configuration = await providerResolver.GetEnabledProviderAsync(userId, ct);
        if (provider is null || configuration is null)
        {
            return;
        }

        var inputJson = JsonSerializer.Serialize(input);
        var cacheKey = ComputeHash(inputJson + SystemPrompt);

        if (force)
        {
            var stale = await dbContext.Summaries
              .Where(s => s.UserId == userId && s.SummaryType == summaryType && s.Scope == scope)
              .ToListAsync(ct);
            if (stale.Count > 0)
            {
                dbContext.Summaries.RemoveRange(stale);
            }
        }
        else
        {
            var existing = await dbContext.Summaries
              .AsNoTracking()
              .FirstOrDefaultAsync(s =>
                s.UserId == userId && s.SummaryType == summaryType
                && s.Scope == scope && s.CacheKey == cacheKey, ct);
            if (existing is not null)
            {
                return;
            }
        }

        try
        {
            var startedAt = DateTime.UtcNow;
            var result = await provider.GenerateAsync(
              configuration, new NarrativeRequest(SystemPrompt, userPrompt), ct);
            var generatedAt = DateTime.UtcNow;
            var providerType = configuration.ProviderType.ToString().ToLowerInvariant();
            var latencyMs = (long)(generatedAt - startedAt).TotalMilliseconds;

            dbContext.LlmCallLogs.Add(new LlmCallLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderType = providerType,
                Model = result.ModelUsed,
                SystemPrompt = SystemPrompt,
                UserPrompt = userPrompt,
                Purpose = $"summary:{summaryType}",
                ResponseRaw = result.Content,
                LatencyMs = latencyMs,
                Success = true,
                CreatedAt = generatedAt
            });

            dbContext.Summaries.Add(new Summary
            {
                SummaryType = summaryType,
                Scope = scope,
                CacheKey = cacheKey,
                Content = result.Content,
                InputSnapshot = inputJson,
                ModelUsed = result.ModelUsed,
                ProviderUsed = providerType,
                TokensUsed = result.TokensUsed,
                UserId = userId,
                GeneratedAt = generatedAt
            });
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
              ex, "Failed to generate {SummaryType} narrative for user {UserId} scope {Scope}.",
              summaryType, userId, scope);
        }
    }

    private async Task<Summary?> GetLatestSummaryAsync(
      string summaryType, string scope, Guid userId, CancellationToken ct)
    {
        return await dbContext.Summaries
          .AsNoTracking()
          .Where(s => s.UserId == userId && s.SummaryType == summaryType && s.Scope == scope)
          .OrderByDescending(s => s.GeneratedAt)
          .FirstOrDefaultAsync(ct);
    }

    private async Task<DashboardNarrativeInput?> BuildDashboardInputAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var last30Start = now.Date.AddDays(-29);
        var transactions = await dbContext.Transactions
          .AsNoTracking()
          .Include(t => t.Category)
          .ThenInclude(c => c.ParentCategory)
          .Where(t => t.UserId == userId && !t.IsDeleted)
          .ToListAsync(ct);

        if (transactions.Count == 0)
        {
            return null;
        }

        var spending = transactions.Where(t => t.Direction == Direction.Debit && !IsExcluded(t)).ToList();
        var income = transactions.Where(t => t.Direction == Direction.Credit).ToList();
        var daysElapsed = Math.Max((now.Date - currentMonthStart.Date).Days + 1, 1);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var mtdSpend = spending
          .Where(t => t.TransactionDate >= currentMonthStart && t.TransactionDate <= now)
          .Sum(t => t.Amount);
        var projectedMonthEnd = Math.Round((mtdSpend / daysElapsed * daysInMonth) / 10m) * 10m;
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthComparableDays = Math.Min(daysElapsed, DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month));
        var previousMonthSamePeriodEnd = previousMonthStart.AddDays(previousMonthComparableDays).AddTicks(-1);
        var sameDaysLastMonth = spending
          .Where(t => t.TransactionDate >= previousMonthStart && t.TransactionDate <= previousMonthSamePeriodEnd)
          .Sum(t => t.Amount);
        var lastMonthStart = currentMonthStart.AddMonths(-1);
        var lastMonthEnd = currentMonthStart.AddTicks(-1);
        var lastMonthTotal = spending
          .Where(t => t.TransactionDate >= lastMonthStart && t.TransactionDate <= lastMonthEnd)
          .Sum(t => t.Amount);
        var rolling30Spend = spending
          .Where(t => t.TransactionDate >= last30Start && t.TransactionDate <= now)
          .Sum(t => t.Amount);
        var rolling30Income = income
          .Where(t => t.TransactionDate >= last30Start && t.TransactionDate <= now)
          .Sum(t => t.Amount);
        var topCategory = spending
          .Where(t => t.TransactionDate >= currentMonthStart && t.TransactionDate <= now)
          .GroupBy(GetCategoryKey)
          .OrderByDescending(g => g.Sum(t => t.Amount))
          .FirstOrDefault();
        var largestTransaction = spending
          .Where(t => t.TransactionDate >= last30Start && t.TransactionDate <= now)
          .OrderByDescending(t => t.Amount)
          .FirstOrDefault();
        var transactionCount = spending
          .Count(t => t.TransactionDate >= last30Start && t.TransactionDate <= now);

        return new DashboardNarrativeInput(
          now.ToString("yyyy-MM-dd"), daysElapsed, daysInMonth,
          mtdSpend, sameDaysLastMonth, projectedMonthEnd, lastMonthTotal, null,
          rolling30Spend, rolling30Income, rolling30Income - rolling30Spend,
          topCategory?.Key ?? "None", topCategory?.Sum(t => t.Amount) ?? 0,
          largestTransaction?.MerchantNormalized ?? "Unknown",
          largestTransaction?.Amount ?? 0,
          largestTransaction is null ? "Unknown" : GetCategoryKey(largestTransaction),
          transactionCount);
    }

    private async Task<MonthlyNarrativeInput?> BuildMonthlyInputAsync(
      Guid userId, int year, int month, CancellationToken ct)
    {
        var transactions = await dbContext.Transactions
          .AsNoTracking()
          .Include(t => t.Category).ThenInclude(c => c.ParentCategory)
          .Where(t => t.UserId == userId && !t.IsDeleted)
          .ToListAsync(ct);

        var spending = transactions.Where(t => t.Direction == Direction.Debit && !IsExcluded(t)).ToList();
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        var previousStart = start.AddMonths(-1);
        var previousEnd = start.AddTicks(-1);
        var current = spending.Where(t => t.TransactionDate >= start && t.TransactionDate <= end).ToList();
        var previous = spending.Where(t => t.TransactionDate >= previousStart && t.TransactionDate <= previousEnd).ToList();

        if (current.Count == 0)
        {
            return null;
        }

        var total = current.Sum(t => t.Amount);
        var previousTotal = previous.Sum(t => t.Amount);
        var rollingAverage = Enumerable.Range(0, 3)
          .Select(offset => spending
            .Where(t => t.TransactionDate >= start.AddMonths(-offset) && t.TransactionDate < start.AddMonths(1 - offset))
            .Sum(t => t.Amount))
          .Average();
        var topCategory = current.GroupBy(GetCategoryKey).OrderByDescending(g => g.Sum(t => t.Amount)).First();
        var previousTopCategoryAmount = previous.Where(t => GetCategoryKey(t) == topCategory.Key).Sum(t => t.Amount);
        var biggestDelta = current
          .GroupBy(GetCategoryKey)
          .Select(g => new
          {
              Name = g.Key,
              Current = g.Sum(t => t.Amount),
              Previous = previous.Where(t => GetCategoryKey(t) == g.Key).Sum(t => t.Amount)
          })
          .Select(r => new
          {
              r.Name,
              Delta = r.Current - r.Previous,
              Percent = r.Previous == 0 ? 0m : Math.Round((r.Current - r.Previous) / r.Previous * 100m, 1)
          })
          .OrderByDescending(r => Math.Abs(r.Delta))
          .First();
        var largestTransaction = current.OrderByDescending(t => t.Amount).First();

        return new MonthlyNarrativeInput(
          total, previousTotal, Math.Round((decimal)rollingAverage, 0),
          topCategory.Key, topCategory.Sum(t => t.Amount), previousTopCategoryAmount,
          biggestDelta.Name, biggestDelta.Delta >= 0 ? "+" : "-",
          Math.Abs(biggestDelta.Delta), Math.Abs(biggestDelta.Percent),
          largestTransaction.MerchantNormalized ?? "Unknown", largestTransaction.Amount,
          GetCategoryKey(largestTransaction), current.Count, 0, 0);
    }

    private async Task<YearlyNarrativeInput?> BuildYearlyInputAsync(
      Guid userId, int year, CancellationToken ct)
    {
        var transactions = await dbContext.Transactions
          .AsNoTracking()
          .Include(t => t.Category).ThenInclude(c => c.ParentCategory)
          .Where(t => t.UserId == userId && !t.IsDeleted)
          .ToListAsync(ct);

        var spending = transactions.Where(t => t.Direction == Direction.Debit && !IsExcluded(t)).ToList();
        var income = transactions.Where(t => t.Direction == Direction.Credit).ToList();
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var yearEnd = year == now.Year ? now : yearStart.AddYears(1).AddTicks(-1);
        var current = spending.Where(t => t.TransactionDate >= yearStart && t.TransactionDate <= yearEnd).ToList();

        if (current.Count == 0)
        {
            return null;
        }

        var ytdTotal = current.Sum(t => t.Amount);
        var daysElapsed = Math.Max((yearEnd.Date - yearStart.Date).Days + 1, 1);
        var dailyAverage = Math.Round(ytdTotal / daysElapsed, 0);
        var previousYearStart = yearStart.AddYears(-1);
        var previousYearEnd = yearEnd.AddYears(-1);
        var previousYtdTotal = spending
          .Where(t => t.TransactionDate >= previousYearStart && t.TransactionDate <= previousYearEnd)
          .Sum(t => t.Amount);
        var top3WithAmounts = current
          .GroupBy(GetCategoryKey)
          .OrderByDescending(g => g.Sum(t => t.Amount))
          .Take(3)
          .Select(g => $"{g.Key} (EUR{Math.Round(g.Sum(t => t.Amount), 0):F0})")
          .ToList();
        var biggestTransaction = current.OrderByDescending(t => t.Amount).First();
        var ytdIncome = income
          .Where(t => t.TransactionDate >= yearStart && t.TransactionDate <= yearEnd)
          .Sum(t => t.Amount);

        return new YearlyNarrativeInput(
          ytdTotal, previousYtdTotal > 0 ? previousYtdTotal : null, dailyAverage,
          string.Join(", ", top3WithAmounts),
          biggestTransaction.MerchantNormalized ?? "Unknown", biggestTransaction.Amount,
          GetCategoryKey(biggestTransaction), ytdIncome, ytdIncome - ytdTotal);
    }

    private async Task<InvestmentNarrativeInput?> BuildInvestmentInputAsync(
      InvestmentAnalyticsService analyticsService, Guid userId, CancellationToken ct)
    {
        try
        {
            var summary = await analyticsService.GetSummaryAsync(userId, ct);
            if (summary.TotalValue == 0)
            {
                return null;
            }

            var accounts = await analyticsService.GetAccountsAsync(userId, ct);
            var largestAccount = accounts.OrderByDescending(a => a.Value).FirstOrDefault();
            var accountTypeSummary = string.Join(", ", accounts
              .GroupBy(a => a.AccountType)
              .OrderByDescending(g => g.Sum(a => a.Value))
              .Select(g => $"{g.Key}: {g.Sum(a => a.Value):N0}"));

            return new InvestmentNarrativeInput(
              TotalValue: summary.TotalValue,
              DayChange: summary.DayChange, DayChangePercent: summary.DayChangePercent,
              YtdChange: summary.YtdChange, YtdChangePercent: summary.YtdChangePercent,
              IbkrValue: summary.IbkrValue,
              IbkrPercent: summary.TotalValue > 0 ? Math.Round(summary.IbkrValue / summary.TotalValue * 100, 1) : 0,
              ManualValue: summary.ManualValue,
              ManualPercent: summary.TotalValue > 0 ? Math.Round(summary.ManualValue / summary.TotalValue * 100, 1) : 0,
              AccountTypeSummary: accountTypeSummary,
              LargestAccountName: largestAccount?.DisplayName ?? "N/A",
              LargestAccountValue: largestAccount?.Value ?? 0,
              DaysSinceManualUpdate: summary.OldestManualUpdateDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build investment narrative input for user {UserId}", userId);
            return null;
        }
    }

    private static string BuildDashboardPrompt(DashboardNarrativeInput input) =>
      $"""
    Write ONE sentence (max 15 words) describing how this month's spending is going.
    Input data:
    - Today: {input.Today}, Days into month: {input.DayOfMonth} of {input.DaysInMonth}
    - MTD spend: EUR{input.MtdSpend:F0}, Same days last month: EUR{input.SameDaysLastMonth:F0}
    - Projected: EUR{input.ProjectedMonthEnd:F0}, Last full month: EUR{input.LastMonthTotal:F0}
    - 30d rolling: spend EUR{input.Rolling30Spend:F0}, income EUR{input.Rolling30Income:F0}
    - Top category MTD: {input.TopCategory} (EUR{input.TopCategoryAmount:F0})
    - Largest txn 30d: {input.LargestMerchant} EUR{input.LargestAmount:F0} ({input.LargestCategory})
    - Txn count 30d: {input.TxnCount}
    Output: ONE sentence. No greeting, no padding.
    """;

    private static string BuildMonthlyPrompt(MonthlyNarrativeInput input, int year, int month) =>
      $"""
    Summarize spending for {new DateTime(year, month, 1):MMMM} {year}.
    - Total: EUR{input.Total:F0}, Previous month: EUR{input.PreviousTotal:F0}, 3mo avg: EUR{input.RollingAvg:F0}
    - Top category: {input.TopCategory} (EUR{input.TopCategoryAmount:F0}), prev EUR{input.PrevTopCategoryAmount:F0}
    - Biggest delta: {input.BiggestDeltaCategory} ({input.DeltaSign}EUR{input.DeltaAmount:F0}, {input.DeltaPercent:F1}%)
    - Largest txn: {input.LargestMerchant} EUR{input.LargestAmount:F0} ({input.LargestCategory})
    - Count: {input.TxnCount}
    Focus on what changed vs recent months. 2-3 sentences.
    """;

    private static string BuildYearlyPrompt(YearlyNarrativeInput input, int year) =>
      $"""
    Summarize YTD spending for {year}.
    - YTD total: EUR{input.YtdTotal:F0}, Previous year same period: {(input.PreviousYtdTotal.HasValue ? $"EUR{input.PreviousYtdTotal.Value:F0}" : "no data")}
    - Daily avg: EUR{input.DailyAvg:F0}
    - Top 3: {input.Top3WithAmounts}
    - Biggest txn: {input.BiggestMerchant} EUR{input.BiggestAmount:F0} ({input.BiggestCategory})
    - Income vs spending: income EUR{input.YtdIncome:F0} / net EUR{input.YtdNet:F0}
    Focus on trajectory and standout patterns. 2-3 sentences.
    """;

    private static string BuildInvestmentPrompt(InvestmentNarrativeInput input)
    {
        var daySign = input.DayChange.HasValue && input.DayChange.Value >= 0 ? "+" : "";
        var ytdSign = input.YtdChange.HasValue && input.YtdChange.Value >= 0 ? "+" : "";
        return $"""
      Write ONE sentence (max 15 words) about the portfolio.
      - Total: EUR{input.TotalValue:N0}
      - Day: {daySign}EUR{input.DayChange:N0} ({input.DayChangePercent:N1}%)
      - YTD: {ytdSign}EUR{input.YtdChange:N0} ({input.YtdChangePercent:N1}%)
      - IBKR: EUR{input.IbkrValue:N0} ({input.IbkrPercent}%), Manual: EUR{input.ManualValue:N0} ({input.ManualPercent}%)
      - Breakdown: {input.AccountTypeSummary}
      - Largest: {input.LargestAccountName} (EUR{input.LargestAccountValue:N0})
      Output: ONE sentence.
      """;
    }

    private static string GetCategoryKey(Transaction t) =>
      t.Category.ParentCategory?.Name ?? t.Category.Name;

    private static bool IsExcluded(Transaction t) =>
      t.Category.ExcludeFromExpenses || t.Category.ParentCategory?.ExcludeFromExpenses == true;

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal record DashboardNarrativeInput(
  string Today, int DayOfMonth, int DaysInMonth,
  decimal MtdSpend, decimal SameDaysLastMonth, decimal ProjectedMonthEnd,
  decimal LastMonthTotal, decimal? SameMonthLastYear,
  decimal Rolling30Spend, decimal Rolling30Income, decimal Net30,
  string TopCategory, decimal TopCategoryAmount,
  string LargestMerchant, decimal LargestAmount, string LargestCategory,
  int TxnCount);

internal record MonthlyNarrativeInput(
  decimal Total, decimal PreviousTotal, decimal RollingAvg,
  string TopCategory, decimal TopCategoryAmount, decimal PrevTopCategoryAmount,
  string BiggestDeltaCategory, string DeltaSign, decimal DeltaAmount, decimal DeltaPercent,
  string LargestMerchant, decimal LargestAmount, string LargestCategory,
  int TxnCount, int OneOffCount, int RecurringCount);

internal record YearlyNarrativeInput(
  decimal YtdTotal, decimal? PreviousYtdTotal, decimal DailyAvg,
  string Top3WithAmounts, string BiggestMerchant, decimal BiggestAmount,
  string BiggestCategory, decimal YtdIncome, decimal YtdNet);

internal record InvestmentNarrativeInput(
  decimal TotalValue, decimal? DayChange, decimal? DayChangePercent,
  decimal? YtdChange, decimal? YtdChangePercent,
  decimal IbkrValue, decimal IbkrPercent,
  decimal ManualValue, decimal ManualPercent,
  string AccountTypeSummary,
  string LargestAccountName, decimal LargestAccountValue,
  int? DaysSinceManualUpdate);
