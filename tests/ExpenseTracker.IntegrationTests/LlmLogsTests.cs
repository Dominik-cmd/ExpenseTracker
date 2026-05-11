using System.Net;
using System.Text.Json;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.IntegrationTests;

public sealed class LlmLogsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
  [Fact]
  public async Task GetLogs_FiltersByPurpose()
  {
    var userId = await GetUserIdAsync();

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
          CreatedAt = DateTime.UtcNow.AddMinutes(-2)
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
          LatencyMs = 20,
          Success = true,
          CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });

      await dbContext.SaveChangesAsync();
    });

    var client = await CreateAuthenticatedClientAsync();
    var response = await client.GetAsync("/api/llm-logs?purpose=summary:dashboard");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal(1, payload.RootElement.GetProperty("totalCount").GetInt32());
    var item = payload.RootElement.GetProperty("items")[0];
    Assert.Equal("summary:dashboard", item.GetProperty("purpose").GetString());
    Assert.Equal("summarize this", item.GetProperty("userPrompt").GetString());
  }
}
