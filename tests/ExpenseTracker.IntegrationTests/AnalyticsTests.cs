using ExpenseTracker.Api.Models;
using System.Net;

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
    Assert.True(payload!.Kpi.Last30Days > 0);
    Assert.NotEmpty(payload.CategoryBreakdown);
    Assert.NotEmpty(payload.DailySpending);
    Assert.NotEmpty(payload.TopMerchants);
    Assert.NotEmpty(payload.RecentTransactions);
    Assert.True(payload.Ytd.Total > 0);
  }
}
