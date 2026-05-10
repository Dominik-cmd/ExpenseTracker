using ExpenseTracker.Application.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseTracker.IntegrationTests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  public const string TestUsername = "dominik";
  public const string TestPassword = "TestPassword123!";
  public const string TestWebhookSecret = "test-webhook-secret";

  private const string JwtSecret = "integration-tests-secret-key-with-sufficient-length";
  private const string JwtIssuer = "ExpenseTracker.IntegrationTests";
  private const string JwtAudience = "ExpenseTracker.IntegrationTests.Client";

  private readonly string _databaseName = $"ExpenseTrackerTests_{Guid.NewGuid():N}";

  public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

  public CustomWebApplicationFactory()
  {
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "data-protection-keys"));
    Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
    Environment.SetEnvironmentVariable("INITIAL_PASSWORD", TestPassword);
    Environment.SetEnvironmentVariable("DATA_PROTECTION_KEY_PATH", Path.Combine(AppContext.BaseDirectory, "data-protection-keys"));
    Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Host=unused;Database=unused");
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Development");
    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
    {
      configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Jwt:Secret"] = JwtSecret,
        ["Jwt:Issuer"] = JwtIssuer,
        ["Jwt:Audience"] = JwtAudience
      });
    });

    builder.ConfigureServices(services =>
    {
      services.RemoveAll(typeof(AppDbContext));
      services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
      services.RemoveAll(typeof(DbContextOptions));
      services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

      var hostedServicesToRemove = services
          .Where(descriptor =>
              descriptor.ServiceType == typeof(IHostedService)
              && (descriptor.ImplementationType == typeof(SmsProcessingBackgroundService)
                  || descriptor.ImplementationType == typeof(NarrativeRegenerationWorker)))
          .ToList();

      foreach (var hostedService in hostedServicesToRemove)
      {
        services.Remove(hostedService);
      }

      services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
    });
  }

  public HttpClient CreateApiClient()
      => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public async Task ResetDatabaseAsync()
  {
    using var scope = Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await dbContext.Database.EnsureDeletedAsync();
    await dbContext.Database.EnsureCreatedAsync();

    var seedDataService = new SeedDataService(dbContext, NullLogger<SeedDataService>.Instance);
    await seedDataService.SeedAsync();

    var webhookSecret = await dbContext.Settings.SingleAsync(x => x.Key == "sms_webhook_secret");
    webhookSecret.Value = TestWebhookSecret;
    webhookSecret.UpdatedAt = DateTime.UtcNow;
    await dbContext.SaveChangesAsync();
  }

  public async Task<HttpClient> CreateAuthenticatedClientAsync()
  {
    var client = CreateApiClient();
    var login = await LoginAsync(client);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
    return client;
  }

  public async Task<LoginResponse> LoginAsync(HttpClient client)
  {
    var response = await client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequest(TestUsername, TestPassword),
        JsonOptions);

    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions)
        ?? throw new InvalidOperationException("Login response was empty.");
  }

  public async Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
  {
    using var scope = Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await action(dbContext);
  }

  public async Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
  {
    using var scope = Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    return await action(dbContext);
  }

  private static JsonSerializerOptions CreateJsonOptions()
  {
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter());
    return options;
  }
}
