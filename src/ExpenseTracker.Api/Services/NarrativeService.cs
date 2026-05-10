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
    ILogger<NarrativeService> logger)
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
        return cached is null ? null : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task<NarrativeResponse?> GetMonthlyNarrativeAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("monthly", $"{year}-{month:D2}", userId, ct);
        return cached is null ? null : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task<NarrativeResponse?> GetYearlyNarrativeAsync(Guid userId, int year, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("yearly", year.ToString(), userId, ct);
        return cached is null ? null : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task RegenerateDashboardNarrativeAsync(Guid userId, bool force, CancellationToken ct)
    {
        var input = await BuildDashboardInputAsync(userId, ct);
        if (input is null) return;
        await GenerateAndStoreAsync(userId, "dashboard", "current", input, BuildDashboardPrompt(input), force, ct);
    }

    public async Task RegenerateMonthlyNarrativeAsync(Guid userId, int year, int month, bool force, CancellationToken ct)
    {
        var input = await BuildMonthlyInputAsync(userId, year, month, ct);
        if (input is null) return;
        await GenerateAndStoreAsync(userId, "monthly", $"{year}-{month:D2}", input, BuildMonthlyPrompt(input, year, month), force, ct);
    }

    public async Task RegenerateYearlyNarrativeAsync(Guid userId, int year, bool force, CancellationToken ct)
    {
        var input = await BuildYearlyInputAsync(userId, year, ct);
        if (input is null) return;
        await GenerateAndStoreAsync(userId, "yearly", year.ToString(), input, BuildYearlyPrompt(input, year), force, ct);
    }

    public async Task<NarrativeResponse?> GetInvestmentNarrativeAsync(Guid userId, CancellationToken ct)
    {
        var cached = await GetLatestSummaryAsync("investments", "current", userId, ct);
        return cached is null ? null : new NarrativeResponse(cached.Content, cached.GeneratedAt, cached.ModelUsed, false);
    }

    public async Task RegenerateInvestmentNarrativeAsync(Guid userId, bool force, CancellationToken ct)
    {
        var analyticsService = new InvestmentAnalyticsService(dbContext);
        var input = await BuildInvestmentInputAsync(analyticsService, userId, ct);
        if (input is null) return;
        await GenerateAndStoreAsync(userId, "investments", "current", input, BuildInvestmentPrompt(input), force, ct);
    }

    private async Task GenerateAndStoreAsync(Guid userId, string summaryType, string scope, object input, string userPrompt, bool force, CancellationToken ct)
    {
        var provider = await providerResolver.GetNarrativeProviderAsync(userId, ct);
        var configuration = await providerResolver.GetEnabledProviderAsync(userId, ct);
        if (provider is null || configuration is null) return;

        var inputJson = JsonSerializer.Serialize(input);
        var cacheKey = ComputeHash(inputJson + SystemPrompt);

        if (force)
        {
            var stale = await dbContext.Summaries
                .Where(s => s.UserId == userId && s.SummaryType == summaryType && s.Scope == scope)
                .ToListAsync(ct);
            if (stale.Count > 0)
                dbContext.Summaries.RemoveRange(stale);
        }
        else
        {
            var existing = await dbContext.Summaries
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId && s.SummaryType == summaryType && s.Scope == scope && s.CacheKey == cacheKey, ct);
            if (existing is not null) return;
        }

        try
        {
            var startedAt = DateTime.UtcNow;
            var result = await provider.GenerateAsync(configuration, new NarrativeRequest(SystemPrompt, userPrompt), ct);
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
            logger.LogError(ex, "Failed to generate {SummaryType} narrative for user {UserId} scope {Scope}.", summaryType, userId, scope);
        }
    }

    private async Task<Summary?> GetLatestSummaryAsync(string summaryType, string scope, Guid userId, CancellationToken ct)
    {
        return await dbContext.Summaries
            .AsNoTracking()
            .Where(summary => summary.UserId == userId && summary.SummaryType == summaryType && summary.Scope == scope)
            .OrderByDescending(summary => summary.GeneratedAt)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<DashboardNarrativeInput?> BuildDashboardInputAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var last30Start = now.Date.AddDays(-29);
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.ParentCategory)
            .Where(transaction => transaction.UserId == userId && !transaction.IsDeleted)
            .ToListAsync(ct);

        if (transactions.Count == 0)
        {
            return null;
        }

        var spending = transactions.Where(transaction => transaction.Direction == Direction.Debit && !IsExcluded(transaction)).ToList();
        var income = transactions.Where(transaction => transaction.Direction == Direction.Credit).ToList();
        var daysElapsed = Math.Max((now.Date - currentMonthStart.Date).Days + 1, 1);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        var mtdSpend = spending.Where(transaction => transaction.TransactionDate >= currentMonthStart && transaction.TransactionDate <= now).Sum(transaction => transaction.Amount);
        var projectedMonthEnd = Math.Round((mtdSpend / daysElapsed * daysInMonth) / 10m) * 10m;

        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthComparableDays = Math.Min(daysElapsed, DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month));
        var previousMonthSamePeriodEnd = previousMonthStart.AddDays(previousMonthComparableDays).AddTicks(-1);
        var sameDaysLastMonth = spending
            .Where(transaction => transaction.TransactionDate >= previousMonthStart && transaction.TransactionDate <= previousMonthSamePeriodEnd)
            .Sum(transaction => transaction.Amount);

        var lastMonthStart = currentMonthStart.AddMonths(-1);
        var lastMonthEnd = currentMonthStart.AddTicks(-1);
        var lastMonthTotal = spending.Where(transaction => transaction.TransactionDate >= lastMonthStart && transaction.TransactionDate <= lastMonthEnd).Sum(transaction => transaction.Amount);

        var sameMonthLastYearStart = currentMonthStart.AddYears(-1);
        var sameMonthLastYearEndDay = Math.Min(now.Day, DateTime.DaysInMonth(now.Year - 1, now.Month));
        var sameMonthLastYearEnd = new DateTime(now.Year - 1, now.Month, sameMonthLastYearEndDay, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(1)
            .AddTicks(-1);
        var sameMonthLastYear = spending
            .Where(transaction => transaction.TransactionDate >= sameMonthLastYearStart && transaction.TransactionDate <= sameMonthLastYearEnd)
            .Sum(transaction => transaction.Amount);

        var rolling30Spend = spending.Where(transaction => transaction.TransactionDate >= last30Start && transaction.TransactionDate <= now).Sum(transaction => transaction.Amount);
        var rolling30Income = income.Where(transaction => transaction.TransactionDate >= last30Start && transaction.TransactionDate <= now).Sum(transaction => transaction.Amount);

        var topCategory = spending
            .Where(transaction => transaction.TransactionDate >= currentMonthStart && transaction.TransactionDate <= now)
            .GroupBy(GetCategoryKey)
            .OrderByDescending(group => group.Sum(transaction => transaction.Amount))
            .FirstOrDefault();

        var largestTransaction = spending
            .Where(transaction => transaction.TransactionDate >= last30Start && transaction.TransactionDate <= now)
            .OrderByDescending(transaction => transaction.Amount)
            .FirstOrDefault();

        var transactionCount = spending.Count(transaction => transaction.TransactionDate >= last30Start && transaction.TransactionDate <= now);

        return new DashboardNarrativeInput(
            now.ToString("yyyy-MM-dd"),
            daysElapsed,
            daysInMonth,
            mtdSpend,
            sameDaysLastMonth,
            projectedMonthEnd,
            lastMonthTotal,
            sameMonthLastYear > 0 ? sameMonthLastYear : null,
            rolling30Spend,
            rolling30Income,
            rolling30Income - rolling30Spend,
            topCategory?.Key ?? "None",
            topCategory?.Sum(transaction => transaction.Amount) ?? 0,
            largestTransaction?.MerchantNormalized ?? "Unknown",
            largestTransaction?.Amount ?? 0,
            largestTransaction is null ? "Unknown" : GetCategoryKey(largestTransaction),
            transactionCount);
    }

    private async Task<MonthlyNarrativeInput?> BuildMonthlyInputAsync(Guid userId, int year, int month, CancellationToken ct)
    {
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.ParentCategory)
            .Where(transaction => transaction.UserId == userId && !transaction.IsDeleted)
            .ToListAsync(ct);

        var spending = transactions.Where(transaction => transaction.Direction == Direction.Debit && !IsExcluded(transaction)).ToList();
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        var previousStart = start.AddMonths(-1);
        var previousEnd = start.AddTicks(-1);

        var current = spending.Where(transaction => transaction.TransactionDate >= start && transaction.TransactionDate <= end).ToList();
        var previous = spending.Where(transaction => transaction.TransactionDate >= previousStart && transaction.TransactionDate <= previousEnd).ToList();
        if (current.Count == 0)
        {
            return null;
        }

        var total = current.Sum(transaction => transaction.Amount);
        var previousTotal = previous.Sum(transaction => transaction.Amount);
        var rollingAverage = Enumerable.Range(0, 3)
            .Select(offset => spending
                .Where(transaction => transaction.TransactionDate >= start.AddMonths(-offset) && transaction.TransactionDate < start.AddMonths(1 - offset))
                .Sum(transaction => transaction.Amount))
            .Average();

        var topCategory = current
            .GroupBy(GetCategoryKey)
            .OrderByDescending(group => group.Sum(transaction => transaction.Amount))
            .First();
        var previousTopCategoryAmount = previous
            .Where(transaction => GetCategoryKey(transaction) == topCategory.Key)
            .Sum(transaction => transaction.Amount);

        var biggestDelta = current
            .GroupBy(GetCategoryKey)
            .Select(group => new
            {
                Name = group.Key,
                Current = group.Sum(transaction => transaction.Amount),
                Previous = previous.Where(transaction => GetCategoryKey(transaction) == group.Key).Sum(transaction => transaction.Amount)
            })
            .Select(result => new
            {
                result.Name,
                Delta = result.Current - result.Previous,
                Percent = result.Previous == 0 ? 0m : Math.Round((result.Current - result.Previous) / result.Previous * 100m, 1)
            })
            .OrderByDescending(result => Math.Abs(result.Delta))
            .First();

        var largestTransaction = current.OrderByDescending(transaction => transaction.Amount).First();

        return new MonthlyNarrativeInput(
            total,
            previousTotal,
            Math.Round((decimal)rollingAverage, 0),
            topCategory.Key,
            topCategory.Sum(transaction => transaction.Amount),
            previousTopCategoryAmount,
            biggestDelta.Name,
            biggestDelta.Delta >= 0 ? "+" : "-",
            Math.Abs(biggestDelta.Delta),
            Math.Abs(biggestDelta.Percent),
            largestTransaction.MerchantNormalized ?? "Unknown",
            largestTransaction.Amount,
            GetCategoryKey(largestTransaction),
            current.Count,
            0,
            0);
    }

    private async Task<YearlyNarrativeInput?> BuildYearlyInputAsync(Guid userId, int year, CancellationToken ct)
    {
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.Category)
            .ThenInclude(category => category.ParentCategory)
            .Where(transaction => transaction.UserId == userId && !transaction.IsDeleted)
            .ToListAsync(ct);

        var spending = transactions.Where(transaction => transaction.Direction == Direction.Debit && !IsExcluded(transaction)).ToList();
        var income = transactions.Where(transaction => transaction.Direction == Direction.Credit).ToList();
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        var yearEnd = year == now.Year ? now : yearStart.AddYears(1).AddTicks(-1);

        var current = spending.Where(transaction => transaction.TransactionDate >= yearStart && transaction.TransactionDate <= yearEnd).ToList();
        if (current.Count == 0)
        {
            return null;
        }

        var ytdTotal = current.Sum(transaction => transaction.Amount);
        var daysElapsed = Math.Max((yearEnd.Date - yearStart.Date).Days + 1, 1);
        var dailyAverage = Math.Round(ytdTotal / daysElapsed, 0);

        var previousYearStart = yearStart.AddYears(-1);
        var previousYearEnd = yearEnd.AddYears(-1);
        var previousYtdTotal = spending
            .Where(transaction => transaction.TransactionDate >= previousYearStart && transaction.TransactionDate <= previousYearEnd)
            .Sum(transaction => transaction.Amount);

        var top3WithAmounts = current
            .GroupBy(GetCategoryKey)
            .OrderByDescending(group => group.Sum(transaction => transaction.Amount))
            .Take(3)
            .Select(group => $"{group.Key} (€{Math.Round(group.Sum(transaction => transaction.Amount), 0):F0})")
            .ToList();

        var biggestTransaction = current.OrderByDescending(transaction => transaction.Amount).First();
        var ytdIncome = income.Where(transaction => transaction.TransactionDate >= yearStart && transaction.TransactionDate <= yearEnd).Sum(transaction => transaction.Amount);

        return new YearlyNarrativeInput(
            ytdTotal,
            previousYtdTotal > 0 ? previousYtdTotal : null,
            dailyAverage,
            string.Join(", ", top3WithAmounts),
            biggestTransaction.MerchantNormalized ?? "Unknown",
            biggestTransaction.Amount,
            GetCategoryKey(biggestTransaction),
            ytdIncome,
            ytdIncome - ytdTotal);
    }

    private static string BuildDashboardPrompt(DashboardNarrativeInput input) =>
        $"""
        Write ONE sentence (max 15 words) describing how this month's spending is going.

        Style requirements:
        - Lead with the takeaway, not the math
        - Don't start with "Your spending is" or "This month is" — start with the observation
        - Reference one specific number or merchant only if it carries the meaning
        - Match the tone of these examples:
          - "Tracking 12% above usual; one big insurance payment is the cause."
          - "Quiet month so far — €566 spent, normal daily pace."
          - "On pace for a high month; dining and travel running hot."
          - "Below normal — last month's insurance bill is gone, baseline spending is steady."
          - "Tracking normally."

        Input data:
        - Today: {input.Today}
        - Days into current month: {input.DayOfMonth} of {input.DaysInMonth}
        - Month-to-date spend: €{input.MtdSpend:F0}
        - Same days last month: €{input.SameDaysLastMonth:F0}
        - Projected month-end: €{input.ProjectedMonthEnd:F0}
        - Last full month total: €{input.LastMonthTotal:F0}
        - 30-day rolling spend: €{input.Rolling30Spend:F0}
        - 30-day rolling income: €{input.Rolling30Income:F0}
        - Top spending category MTD: {input.TopCategory} (€{input.TopCategoryAmount:F0})
        - Largest single transaction last 30d: {input.LargestMerchant} €{input.LargestAmount:F0} ({input.LargestCategory})
        - Transactions last 30d: {input.TxnCount}

        Output: ONE sentence. No greeting, no padding, no second sentence.
        """;

    private static string BuildMonthlyPrompt(MonthlyNarrativeInput input, int year, int month) =>
        $"""
        Summarize the spending pattern for {new DateTime(year, month, 1):MMMM} {year}.

        Input data:
        - Total spending: €{input.Total:F0}
        - Previous month total: €{input.PreviousTotal:F0}
        - 3-month rolling average: €{input.RollingAvg:F0}
        - Largest category: {input.TopCategory} (€{input.TopCategoryAmount:F0}), prev month €{input.PrevTopCategoryAmount:F0}
        - Biggest delta vs previous month: {input.BiggestDeltaCategory} ({input.DeltaSign}€{input.DeltaAmount:F0}, {input.DeltaPercent:F1}%)
        - Largest single transaction: {input.LargestMerchant} €{input.LargestAmount:F0} ({input.LargestCategory})
        - Number of transactions: {input.TxnCount}

        Focus on: what changed compared to recent months, what drove the change, was there a one-off event. 2-3 sentences.
        """;

    private static string BuildYearlyPrompt(YearlyNarrativeInput input, int year) =>
        $"""
        Summarize the year-to-date spending pattern for {year}.

        Input data:
        - YTD total: €{input.YtdTotal:F0}
        - Previous year same period: {(input.PreviousYtdTotal.HasValue ? $"€{input.PreviousYtdTotal.Value:F0}" : "no data")}
        - YTD daily average: €{input.DailyAvg:F0}
        - Top 3 categories YTD: {input.Top3WithAmounts}
        - Biggest single transaction YTD: {input.BiggestMerchant} €{input.BiggestAmount:F0} ({input.BiggestCategory})
        - Income vs spending YTD: income €{input.YtdIncome:F0} / net €{input.YtdNet:F0}

        Focus on: trajectory of the year, major categories, and any standout transaction or net-flow pattern. 2-3 sentences.
        """;

    private async Task<InvestmentNarrativeInput?> BuildInvestmentInputAsync(InvestmentAnalyticsService analyticsService, Guid userId, CancellationToken ct)
    {
        try
        {
            var summary = await analyticsService.GetSummaryAsync(userId, ct);
            if (summary.TotalValue == 0) return null;

            var accounts = await analyticsService.GetAccountsAsync(userId, ct);
            var largestAccount = accounts.OrderByDescending(a => a.Value).FirstOrDefault();
            var accountTypeSummary = string.Join(", ", accounts
                .GroupBy(a => a.AccountType)
                .OrderByDescending(g => g.Sum(a => a.Value))
                .Select(g => $"{g.Key}: {g.Sum(a => a.Value):N0}"));

            return new InvestmentNarrativeInput(
                TotalValue: summary.TotalValue,
                DayChange: summary.DayChange,
                DayChangePercent: summary.DayChangePercent,
                YtdChange: summary.YtdChange,
                YtdChangePercent: summary.YtdChangePercent,
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

    private static string BuildInvestmentPrompt(InvestmentNarrativeInput input)
    {
        var daySign = input.DayChange.HasValue && input.DayChange.Value >= 0 ? "+" : "";
        var ytdSign = input.YtdChange.HasValue && input.YtdChange.Value >= 0 ? "+" : "";

        return $"""
            Write ONE sentence (max 15 words) describing the investment portfolio status.

            Style requirements:
            - Lead with the takeaway, not the math
            - Don't start with "Your portfolio is" — start with the observation
            - Reference one specific account or trend if it carries the meaning
            - Match the tone of these examples:
              - "Up 8% YTD, mostly driven by IBKR holdings; manual accounts steady."
              - "Mixed picture — brokerage gains offset by stale crypto balance."
              - "Down 3% this month following the broader market correction."
              - "Allocation tilting toward cash; manual savings growing while IBKR is flat."

            Input data:
            - Total value: EUR{input.TotalValue:N0}
            - Day change: {daySign}EUR{input.DayChange:N0} ({input.DayChangePercent:N1}%)
            - YTD change: {ytdSign}EUR{input.YtdChange:N0} ({input.YtdChangePercent:N1}%)
            - IBKR value: EUR{input.IbkrValue:N0} ({input.IbkrPercent}%)
            - Manual value: EUR{input.ManualValue:N0} ({input.ManualPercent}%)
            - Account type breakdown: {input.AccountTypeSummary}
            - Largest single account: {input.LargestAccountName} (EUR{input.LargestAccountValue:N0})
            - Days since most recent manual balance update: {input.DaysSinceManualUpdate?.ToString() ?? "N/A"}

            Output: ONE sentence. No greeting, no padding.
            """;
    }

    private static string GetCategoryKey(Transaction transaction)
        => transaction.Category.ParentCategory?.Name ?? transaction.Category.Name;

    private static bool IsExcluded(Transaction transaction)
        => transaction.Category.ExcludeFromExpenses || transaction.Category.ParentCategory?.ExcludeFromExpenses == true;

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record NarrativeResponse(string Content, DateTime GeneratedAt, string ModelUsed, bool IsStale);

internal record DashboardNarrativeInput(
    string Today,
    int DayOfMonth,
    int DaysInMonth,
    decimal MtdSpend,
    decimal SameDaysLastMonth,
    decimal ProjectedMonthEnd,
    decimal LastMonthTotal,
    decimal? SameMonthLastYear,
    decimal Rolling30Spend,
    decimal Rolling30Income,
    decimal Net30,
    string TopCategory,
    decimal TopCategoryAmount,
    string LargestMerchant,
    decimal LargestAmount,
    string LargestCategory,
    int TxnCount);

internal record MonthlyNarrativeInput(
    decimal Total,
    decimal PreviousTotal,
    decimal RollingAvg,
    string TopCategory,
    decimal TopCategoryAmount,
    decimal PrevTopCategoryAmount,
    string BiggestDeltaCategory,
    string DeltaSign,
    decimal DeltaAmount,
    decimal DeltaPercent,
    string LargestMerchant,
    decimal LargestAmount,
    string LargestCategory,
    int TxnCount,
    int OneOffCount,
    int RecurringCount);

internal record YearlyNarrativeInput(
    decimal YtdTotal,
    decimal? PreviousYtdTotal,
    decimal DailyAvg,
    string Top3WithAmounts,
    string BiggestMerchant,
    decimal BiggestAmount,
    string BiggestCategory,
    decimal YtdIncome,
    decimal YtdNet);

internal record InvestmentNarrativeInput(
    decimal TotalValue,
    decimal? DayChange, decimal? DayChangePercent,
    decimal? YtdChange, decimal? YtdChangePercent,
    decimal IbkrValue, decimal IbkrPercent,
    decimal ManualValue, decimal ManualPercent,
    string AccountTypeSummary,
    string LargestAccountName, decimal LargestAccountValue,
    int? DaysSinceManualUpdate);
