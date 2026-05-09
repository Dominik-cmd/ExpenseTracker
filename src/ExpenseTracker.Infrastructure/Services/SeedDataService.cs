using System.Security.Cryptography;
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure;

public sealed class SeedDataService : IHostedService
{
    private const string DefaultInitialPassword = "ChangeMeNow!";

    private readonly AppDbContext _dbContext;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(AppDbContext dbContext, ILogger<SeedDataService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => SeedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var initialPassword = Environment.GetEnvironmentVariable("INITIAL_PASSWORD");
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            initialPassword = DefaultInitialPassword;
            _logger.LogWarning("INITIAL_PASSWORD was not configured. Using the default bootstrap password.");
        }

        await SeedUserAsync(initialPassword, cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedLlmProvidersAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUserAsync(string initialPassword, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users.SingleOrDefaultAsync(user => user.Username == "dominik", cancellationToken);
        if (existingUser is not null)
        {
            return;
        }

        _dbContext.Users.Add(new User
        {
            Username = "dominik",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialPassword, workFactor: 12)
        });
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var existingCategories = await _dbContext.Categories.ToDictionaryAsync(category => (category.Name, category.ParentCategoryId), cancellationToken);

        var topLevelDefinitions = new (string Name, int SortOrder, bool IsSystem)[]
        {
            ("Housing", 1, false),
            ("Utilities", 2, false),
            ("Groceries", 3, false),
            ("Dining", 4, false),
            ("Transportation", 5, false),
            ("Fuel", 6, false),
            ("Healthcare", 7, false),
            ("Insurance", 8, false),
            ("Entertainment", 9, false),
            ("Subscriptions", 10, false),
            ("Shopping", 11, false),
            ("Education", 12, false),
            ("Personal Care", 13, false),
            ("Gifts", 14, false),
            ("Travel", 15, false),
            ("Savings", 16, false),
            ("Investments", 17, false),
            ("Taxes", 18, false),
            ("Income", 19, true),
            ("Uncategorized", 99, true)
        };

        foreach (var definition in topLevelDefinitions)
        {
            if (existingCategories.ContainsKey((definition.Name, null)))
            {
                continue;
            }

            var category = new Category
            {
                Name = definition.Name,
                SortOrder = definition.SortOrder,
                IsSystem = definition.IsSystem
            };

            _dbContext.Categories.Add(category);
            existingCategories[(definition.Name, null)] = category;
        }

        var subcategoryDefinitions = new (string Parent, string Name, int SortOrder)[]
        {
            ("Groceries", "Mercator", 1),
            ("Groceries", "Hofer", 2),
            ("Groceries", "Lidl", 3),
            ("Groceries", "Spar", 4),
            ("Groceries", "Tus", 5),
            ("Fuel", "Petrol", 1),
            ("Fuel", "OMV", 2),
            ("Subscriptions", "Netflix", 1),
            ("Subscriptions", "Spotify", 2)
        };

        foreach (var definition in subcategoryDefinitions)
        {
            if (!existingCategories.TryGetValue((definition.Parent, null), out var parentCategory) || existingCategories.ContainsKey((definition.Name, parentCategory.Id)))
            {
                continue;
            }

            var category = new Category
            {
                Name = definition.Name,
                ParentCategoryId = parentCategory.Id,
                SortOrder = definition.SortOrder
            };

            _dbContext.Categories.Add(category);
            existingCategories[(definition.Name, parentCategory.Id)] = category;
        }
    }

    private async Task SeedLlmProvidersAsync(CancellationToken cancellationToken)
    {
        var existingTypes = await _dbContext.LlmProviders.Select(provider => provider.ProviderType).ToHashSetAsync(cancellationToken);
        var providers = new (LlmProviderType Type, string Name, string Model)[]
        {
            (LlmProviderType.OpenAi, "OpenAI", "GPT-4o"),
            (LlmProviderType.Anthropic, "Anthropic", "claude-sonnet-4-20250514"),
            (LlmProviderType.Gemini, "Gemini", "gemini-2.0-flash")
        };

        foreach (var provider in providers)
        {
            if (existingTypes.Contains(provider.Type))
            {
                continue;
            }

            _dbContext.LlmProviders.Add(new LlmProvider
            {
                ProviderType = provider.Type,
                Name = provider.Name,
                Model = provider.Model,
                IsEnabled = false,
                ApiKeyEncrypted = null
            });
        }
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await _dbContext.Settings.Select(setting => setting.Key).ToHashSetAsync(cancellationToken);

        if (!existingKeys.Contains("sms_webhook_secret"))
        {
            var secret = GenerateUrlSafeSecret();
            _dbContext.Settings.Add(new Setting { Key = "sms_webhook_secret", Value = secret });
            _logger.LogInformation("Seeded sms_webhook_secret: {SmsWebhookSecret}", secret);
        }

        if (!existingKeys.Contains("sms_senders"))
        {
            _dbContext.Settings.Add(new Setting
            {
                Key = "sms_senders",
                Value = JsonSerializer.Serialize(new[] { "OTP banka", "OTP", "OTP Banka" })
            });
        }
    }

    private static string GenerateUrlSafeSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
