using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Investments;
using ExpenseTracker.Infrastructure.Investments.Ibkr;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    builder.Configuration["Jwt:Secret"] = jwtSecret;
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
    ?? "Host=localhost;Port=5432;Database=expensetracker;Username=postgres;Password=postgres";

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(Channel.CreateUnbounded<Guid>());
builder.Services.AddSingleton(Channel.CreateUnbounded<NarrativeRegenerationRequest>());
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<OtpBankaSmsParser>();
builder.Services.AddScoped<NarrativeService>();
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

// Investment services
builder.Services.AddScoped<InvestmentAnalyticsService>();
builder.Services.AddScoped<PortfolioHistoryService>();
builder.Services.AddScoped<IbkrFlexClient>();
builder.Services.AddScoped<IbkrFlexParser>();
builder.Services.AddScoped<IbkrFlexProvider>();
builder.Services.AddScoped<IbkrPersistenceService>();
builder.Services.AddScoped<ManualInvestmentProvider>();
builder.Services.AddHostedService<InvestmentSyncWorker>();
builder.Services.AddHttpClient("IbkrFlex");

var keyPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEY_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new JwtService(builder.Configuration).GetValidationParameters();
    });

builder.Services.AddAuthorization();
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
});

var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(frontendOrigin))
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // EnsureCreated is safe here: it's a no-op if the DB already exists.
    // We don't use migrations, so this must run in all environments.
    await dbContext.Database.EnsureCreatedAsync();

    await scope.ServiceProvider.GetRequiredService<SeedDataService>().StartAsync(CancellationToken.None);
}

app.UseCors();
app.UseMiddleware<ExpenseTracker.Api.Middleware.RequestTimingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program;
