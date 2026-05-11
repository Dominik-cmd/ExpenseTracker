using System.Net;
using System.Text.Json;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;

namespace ExpenseTracker.IntegrationTests;

public sealed class AnalyticsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
  [Fact]
  public async Task GetDashboard_ReturnsPopulatedResponse()
  {
    var groceriesId = await GetCategoryIdAsync("Groceries");
    var fuelId = await GetCategoryIdAsync("Fuel");
    await SeedTransactionAsync(groceriesId, 15.40m, "MERCATOR", DateTime.UtcNow.AddDays(-1));
    await SeedTransactionAsync(groceriesId, 31.25m, "SPAR", DateTime.UtcNow.AddDays(-4));
    await SeedTransactionAsync(fuelId, 54.10m, "PETROL", DateTime.UtcNow.AddDays(-8));
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.GetAsync("/api/analytics/dashboard");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await ReadAsAsync<DashboardResponse>(response);
    Assert.NotNull(payload);
    Assert.NotEmpty(payload!.CategoryLeaderboard);
    Assert.NotEmpty(payload.RecentTransactions);
    Assert.NotNull(payload.Income);
  }

  [Fact]
  public async Task GetDashboardStrip_ReturnsAggregates()
  {
    var groceriesId = await GetCategoryIdAsync("Groceries");
    var fuelId = await GetCategoryIdAsync("Fuel");
    var incomeId = await GetCategoryIdAsync("Income");
    await SeedTransactionAsync(groceriesId, 15.40m, "MERCATOR", DateTime.UtcNow.AddDays(-1));
    await SeedTransactionAsync(fuelId, 54.10m, "PETROL", DateTime.UtcNow.AddDays(-8));
    await SeedTransactionAsync(incomeId, 500m, "EMPLOYER", DateTime.UtcNow.AddDays(-3), Direction.Credit, TransactionType.TransferIn);
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.GetAsync("/api/analytics/dashboard/strip");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await ReadAsAsync<DashboardStrip>(response);
    Assert.NotNull(payload);
    Assert.True(payload!.MonthToDate > 0);
    Assert.True(payload.OnPace > 0);
    Assert.Equal(500m, payload.NetLast30Income);
    Assert.True(payload.NetLast30Spending > 0);
    Assert.Equal(payload.NetLast30Income - payload.NetLast30Spending, payload.NetLast30);
  }

  [Fact]
  public async Task GetDashboardNarrative_ReturnsLatestCachedSummary()
  {
    var userId = await GetUserIdAsync();
    await Factory.ExecuteDbContextAsync(async dbContext =>
    {
      dbContext.Summaries.AddRange(
        new Summary
        {
          UserId = userId,
          SummaryType = "dashboard",
          Scope = "current",
          CacheKey = "older",
          Content = "Older narrative.",
          InputSnapshot = "{}",
          ModelUsed = "model-a",
          ProviderUsed = "openai",
          GeneratedAt = DateTime.UtcNow.AddMinutes(-10)
        },
        new Summary
        {
          UserId = userId,
          SummaryType = "dashboard",
          Scope = "current",
          CacheKey = "newer",
          Content = "Latest narrative.",
          InputSnapshot = "{}",
          ModelUsed = "model-b",
          ProviderUsed = "openai",
          GeneratedAt = DateTime.UtcNow
        });
      await dbContext.SaveChangesAsync();
    });

    var client = await CreateAuthenticatedClientAsync();
    var response = await client.GetAsync("/api/analytics/dashboard/narrative");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await ReadAsAsync<NarrativeResponse>(response);
    Assert.NotNull(payload);
    Assert.Equal("Latest narrative.", payload!.Content);
    Assert.Equal("model-b", payload.ModelUsed);
    Assert.False(payload.IsStale);
  }

  [Fact]
  public async Task RegenerateNarratives_ReturnsOk_WhenNoProviderIsConfigured()
  {
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.PostAsync("/api/analytics/regenerate-narratives", content: null);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task GetCostSummary_ReturnsMonthlyUsage()
  {
    var userId = await GetUserIdAsync();
    var now = DateTime.UtcNow;
    var previousMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1);

    await Factory.ExecuteDbContextAsync(async dbContext =>
    {
      dbContext.LlmCallLogs.AddRange(
        new LlmCallLog
        {
          Id = Guid.NewGuid(),
          UserId = userId,
          ProviderType = "openai",
          Model = "gpt-4o-mini",
          SystemPrompt = "system",
          UserPrompt = "categorize this",
          Purpose = "categorize",
          ResponseRaw = "{}",
          LatencyMs = 10,
          Success = true,
          CreatedAt = now.AddDays(-1)
        },
        new LlmCallLog
        {
          Id = Guid.NewGuid(),
          UserId = userId,
          ProviderType = "openai",
          Model = "gpt-4o-mini",
          SystemPrompt = "system",
          UserPrompt = "summarize this",
          Purpose = "summary:dashboard",
          ResponseRaw = "Narrative",
          LatencyMs = 25,
          Success = true,
          CreatedAt = now.AddDays(-1)
        },
        new LlmCallLog
        {
          Id = Guid.NewGuid(),
          UserId = userId,
          ProviderType = "openai",
          Model = "gpt-4o-mini",
          SystemPrompt = "system",
          UserPrompt = "failed summarize this",
          Purpose = "summary:monthly",
          ResponseRaw = "Narrative",
          LatencyMs = 15,
          Success = false,
          CreatedAt = now.AddDays(-1)
        },
        new LlmCallLog
        {
          Id = Guid.NewGuid(),
          UserId = userId,
          ProviderType = "openai",
          Model = "gpt-4o-mini",
          SystemPrompt = "system",
          UserPrompt = "old call",
          Purpose = "categorize",
          ResponseRaw = "{}",
          LatencyMs = 5,
          Success = true,
          CreatedAt = previousMonth
        });

      dbContext.Summaries.AddRange(
        new Summary
        {
          UserId = userId,
          SummaryType = "dashboard",
          Scope = "current",
          CacheKey = "current-month",
          Content = "Current month summary",
          InputSnapshot = "{}",
          ModelUsed = "gpt-4o-mini",
          ProviderUsed = "openai",
          TokensUsed = 123,
          GeneratedAt = now.AddDays(-1)
        },
        new Summary
        {
          UserId = userId,
          SummaryType = "dashboard",
          Scope = "previous",
          CacheKey = "previous-month",
          Content = "Previous month summary",
          InputSnapshot = "{}",
          ModelUsed = "gpt-4o-mini",
          ProviderUsed = "openai",
          TokensUsed = 999,
          GeneratedAt = previousMonth
        });

      await dbContext.SaveChangesAsync();
    });

    var client = await CreateAuthenticatedClientAsync();
    var response = await client.GetAsync("/api/analytics/cost-summary");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal(1, payload.RootElement.GetProperty("categorization").GetProperty("count").GetInt32());
    Assert.Equal(0, payload.RootElement.GetProperty("categorization").GetProperty("tokens").GetInt32());
    Assert.Equal(1, payload.RootElement.GetProperty("narrative").GetProperty("count").GetInt32());
    Assert.Equal(123, payload.RootElement.GetProperty("narrative").GetProperty("tokens").GetInt32());
    Assert.Equal(2, payload.RootElement.GetProperty("totalCalls").GetInt32());
  }
}
