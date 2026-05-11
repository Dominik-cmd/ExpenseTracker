using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Services;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Data.Repositories;
using ExpenseTracker.Infrastructure.Investments;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using ExpenseTracker.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
  .ReadFrom.Configuration(context.Configuration)
  .ReadFrom.Services(services)
  .Enrich.FromLogContext()
  .Enrich.WithMachineName()
  .Enrich.WithEnvironmentName());

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
  builder.Configuration["Jwt:Secret"] = jwtSecret;
}

// Fail-safe: prevent production startup with default/weak JWT secret
if (!builder.Environment.IsDevelopment())
{
  var configuredSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
  if (configuredSecret.Length < 32 ||
      configuredSecret.StartsWith("ReplaceWith", StringComparison.OrdinalIgnoreCase) ||
      configuredSecret.Contains("development", StringComparison.OrdinalIgnoreCase))
  {
    throw new InvalidOperationException(
      "JWT_SECRET must be set to a strong random value (minimum 32 characters) in production.");
  }
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]))
{
  builder.Configuration["Jwt:Issuer"] = "ExpenseTracker.Api";
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
{
  builder.Configuration["Jwt:Audience"] = "ExpenseTracker.Client";
}

var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
  ?? builder.Configuration.GetConnectionString("DefaultConnection")
  ?? throw new InvalidOperationException(
    "Database connection string is not configured. Set the DB_CONNECTION_STRING environment variable or configure ConnectionStrings:DefaultConnection in appsettings.json.");

builder.Services.AddControllers()
  .AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddMemoryCache();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddSingleton(Channel.CreateUnbounded<NarrativeRegenerationRequest>());

// Repository registrations
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IMerchantRuleRepository, MerchantRuleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISettingRepository, SettingRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IRawMessageRepository, RawMessageRepository>();
builder.Services.AddScoped<ILlmCallLogRepository, LlmCallLogRepository>();
builder.Services.AddScoped<ILlmProviderRepository, LlmProviderRepository>();
builder.Services.AddScoped<ISummaryRepository, SummaryRepository>();
builder.Services.AddScoped<IInvestmentRepository, InvestmentRepository>();

// Application service registrations
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IMerchantRuleService, MerchantRuleService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// Application service registrations (continued)
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ILlmLogService, LlmLogService>();
builder.Services.AddScoped<ILlmProviderService, LlmProviderService>();
builder.Services.AddScoped<IRawMessageService, RawMessageService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();

// Infrastructure service registrations
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ISeedDataService, SeedDataService>();
builder.Services.AddScoped<OtpBankaSmsParser>();
builder.Services.AddScoped<NarrativeService>();
builder.Services.AddScoped<INarrativeService>(sp => sp.GetRequiredService<NarrativeService>());
builder.Services.AddScoped<ILlmProviderResolver, LlmProviderResolver>();
builder.Services.AddScoped<ILlmCategorizationProvider, OpenAiCategorizationProvider>();
builder.Services.AddScoped<ILlmCategorizationProvider, AnthropicCategorizationProvider>();
builder.Services.AddScoped<ILlmCategorizationProvider, GeminiCategorizationProvider>();
builder.Services.AddScoped<ILlmNarrativeProvider, OpenAiCategorizationProvider>();
builder.Services.AddScoped<ILlmNarrativeProvider, AnthropicCategorizationProvider>();
builder.Services.AddScoped<ILlmNarrativeProvider, GeminiCategorizationProvider>();
builder.Services.AddHttpClient("OpenAi");
builder.Services.AddHttpClient("Anthropic");
builder.Services.AddHttpClient("Gemini");
builder.Services.AddHostedService<SmsProcessingBackgroundService>();
builder.Services.AddHostedService<NarrativeRegenerationWorker>();
builder.Services.AddHostedService<DailyNarrativeWorker>();

// Api-layer services (depend on infrastructure directly)
builder.Services.AddScoped<IInvestmentService, InvestmentService>();
builder.Services.AddScoped<IInvestmentProviderService, InvestmentProviderService>();
builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();

// Investment services
builder.Services.AddScoped<InvestmentAnalyticsService>();
builder.Services.AddScoped<PortfolioHistoryService>();
builder.Services.AddSingleton<IbkrRateLimiter>();
builder.Services.AddScoped<IbkrFlexClient>();
builder.Services.AddScoped<IbkrFlexParser>();
builder.Services.AddScoped<IbkrFlexProvider>();
builder.Services.AddScoped<IbkrPersistenceService>();
builder.Services.AddScoped<ManualInvestmentProvider>();
builder.Services.AddHostedService<InvestmentSyncWorker>();
builder.Services.AddHttpClient("IbkrFlex", client =>
{
  client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ExpenseTracker/1.0)");
});

var keyPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEY_PATH")
  ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
  .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.MapInboundClaims = false;
    options.TokenValidationParameters =
      new JwtService(builder.Configuration).GetValidationParameters();
  });

builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
  options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
  options.AddPolicy(RateLimitingMiddleware.LoginPolicyName, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 5,
        Window = TimeSpan.FromMinutes(15),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
      }));
  options.AddPolicy(RateLimitingMiddleware.WebhookPolicyName, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
      partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
      factory: _ => new FixedWindowRateLimiterOptions
      {
        PermitLimit = 60,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
      }));
});

var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    if (string.IsNullOrWhiteSpace(frontendOrigin))
    {
      // In development allow localhost origins; never allow any origin.
      policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
        .AllowAnyHeader().AllowAnyMethod();
      return;
    }

    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod();
  });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
  var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
  await dbContext.Database.EnsureCreatedAsync();
  await scope.ServiceProvider.GetRequiredService<ISeedDataService>().StartAsync(CancellationToken.None);
}

app.UseSerilogRequestLogging(options =>
{
  options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
  {
    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
    diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
  };
});
app.UseExceptionHandler();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
  ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

try
{
  Log.Information("ExpenseTracker starting up");
  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
  Log.CloseAndFlush();
}

public partial class Program;
