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

        await MigrateColumnsAsync(cancellationToken);
        var user = await SeedUserAsync(initialPassword, cancellationToken);
        if (user is not null)
        {
            await SeedForUserAsync(user, cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Seeds default categories, LLM providers, and settings for a specific user.</summary>
    public async Task SeedForUserAsync(User user, CancellationToken cancellationToken = default)
    {
        await SeedCategoriesAsync(user.Id, cancellationToken);
        await SeedLlmProvidersAsync(user.Id, cancellationToken);
        await SeedSettingsAsync(user.Id, cancellationToken);
    }

    private async Task MigrateColumnsAsync(CancellationToken cancellationToken)
    {
        // Skip raw SQL migrations for non-PostgreSQL providers (e.g. InMemory for tests)
        if (!_dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            return;
        }

        // Existing column migrations
        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE categories ADD COLUMN IF NOT EXISTS exclude_from_expenses BOOLEAN NOT NULL DEFAULT FALSE",
            cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE categories ADD COLUMN IF NOT EXISTS exclude_from_income BOOLEAN NOT NULL DEFAULT FALSE",
            cancellationToken);

        // Add is_admin column to users and make first user admin
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='is_admin') THEN
                ALTER TABLE users ADD COLUMN is_admin BOOLEAN NOT NULL DEFAULT FALSE;
                UPDATE users SET is_admin = TRUE WHERE id = (SELECT id FROM users ORDER BY created_at LIMIT 1);
              END IF;
            END $$;
            """, cancellationToken);

        // Multi-user migration: add user_id to all user-scoped tables, backfill with first user
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DO $$
            DECLARE first_user_id UUID;
            DECLARE settings_pk_name TEXT;
            BEGIN
              SELECT id INTO first_user_id FROM users ORDER BY created_at LIMIT 1;

              -- categories
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='categories' AND column_name='user_id') THEN
                ALTER TABLE categories ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE categories SET user_id = first_user_id;
                END IF;
                ALTER TABLE categories ALTER COLUMN user_id SET NOT NULL;
                ALTER TABLE categories ADD CONSTRAINT fk_categories_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
              END IF;

              -- merchant_rules
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='merchant_rules' AND column_name='user_id') THEN
                ALTER TABLE merchant_rules ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE merchant_rules SET user_id = first_user_id;
                END IF;
                ALTER TABLE merchant_rules ALTER COLUMN user_id SET NOT NULL;
                ALTER TABLE merchant_rules ADD CONSTRAINT fk_merchant_rules_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
              END IF;

              -- raw_messages
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='raw_messages' AND column_name='user_id') THEN
                ALTER TABLE raw_messages ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE raw_messages SET user_id = first_user_id;
                END IF;
                ALTER TABLE raw_messages ALTER COLUMN user_id SET NOT NULL;
                ALTER TABLE raw_messages ADD CONSTRAINT fk_raw_messages_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
              END IF;

              -- llm_call_logs
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='llm_call_logs' AND column_name='user_id') THEN
                ALTER TABLE llm_call_logs ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE llm_call_logs SET user_id = first_user_id;
                END IF;
                ALTER TABLE llm_call_logs ALTER COLUMN user_id SET NOT NULL;
              END IF;

              -- llm_providers
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='llm_providers' AND column_name='user_id') THEN
                ALTER TABLE llm_providers ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE llm_providers SET user_id = first_user_id;
                END IF;
                ALTER TABLE llm_providers ALTER COLUMN user_id SET NOT NULL;
                ALTER TABLE llm_providers ADD CONSTRAINT fk_llm_providers_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
              END IF;

              -- settings: needs PK change from (key) to (key, user_id)
              IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='settings' AND column_name='user_id') THEN
                ALTER TABLE settings ADD COLUMN user_id UUID;
                IF first_user_id IS NOT NULL THEN
                  UPDATE settings SET user_id = first_user_id;
                END IF;
                ALTER TABLE settings ALTER COLUMN user_id SET NOT NULL;
                SELECT conname INTO settings_pk_name
                  FROM pg_constraint
                  WHERE conrelid = 'settings'::regclass AND contype = 'p';
                IF settings_pk_name IS NOT NULL THEN
                  EXECUTE 'ALTER TABLE settings DROP CONSTRAINT ' || quote_ident(settings_pk_name);
                END IF;
                ALTER TABLE settings ADD PRIMARY KEY (key, user_id);
                ALTER TABLE settings ADD CONSTRAINT fk_settings_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;
              END IF;
            END $$;
            """, cancellationToken);

        // Update unique indexes to include user_id
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DO $$
            BEGIN
              -- merchant_rules: change unique(merchant_normalized) -> unique(user_id, merchant_normalized)
              IF EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_merchant_rules_merchant_normalized') THEN
                DROP INDEX IF EXISTS ix_merchant_rules_merchant_normalized;
                DROP INDEX IF EXISTS "IX_merchant_rules_merchant_normalized";
              END IF;
              IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_merchant_rules_user_id_merchant_normalized') THEN
                CREATE UNIQUE INDEX ix_merchant_rules_user_id_merchant_normalized ON merchant_rules (user_id, merchant_normalized);
              END IF;

              -- categories: change unique(name, parent_category_id) -> unique(user_id, name, parent_category_id)
              IF EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_categories_name_parent_category_id') THEN
                DROP INDEX IF EXISTS ix_categories_name_parent_category_id;
                DROP INDEX IF EXISTS "IX_categories_name_parent_category_id";
              END IF;
              IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_categories_user_id_name_parent_category_id') THEN
                CREATE UNIQUE INDEX ix_categories_user_id_name_parent_category_id ON categories (user_id, name, parent_category_id) WHERE parent_category_id IS NOT NULL;
              END IF;
              IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_categories_user_id_name') THEN
                CREATE UNIQUE INDEX ix_categories_user_id_name ON categories (user_id, name) WHERE parent_category_id IS NULL;
              END IF;

              -- llm_providers: change unique(is_enabled) -> unique(user_id, is_enabled)
              IF EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_llm_providers_is_enabled') THEN
                DROP INDEX IF EXISTS ix_llm_providers_is_enabled;
                DROP INDEX IF EXISTS "IX_llm_providers_is_enabled";
              END IF;
              IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE lower(indexname)='ix_llm_providers_user_id_is_enabled') THEN
                CREATE UNIQUE INDEX ix_llm_providers_user_id_is_enabled ON llm_providers (user_id) WHERE is_enabled = true;
              END IF;
            END $$;
            """, cancellationToken);

        await _dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE llm_call_logs ADD COLUMN IF NOT EXISTS purpose text NOT NULL DEFAULT 'categorize';",
            cancellationToken);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS summaries (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                summary_type text NOT NULL,
                scope text NOT NULL,
                cache_key text NOT NULL,
                content text NOT NULL,
                input_snapshot text NOT NULL DEFAULT '',
                model_used text NOT NULL DEFAULT '',
                provider_used text NOT NULL DEFAULT '',
                tokens_used int,
                user_id uuid NOT NULL,
                generated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_summaries_lookup ON summaries (user_id, summary_type, scope, cache_key);
            CREATE INDEX IF NOT EXISTS ix_summaries_scope ON summaries (user_id, summary_type, scope, generated_at DESC);
            CREATE INDEX IF NOT EXISTS ix_summaries_user_id ON summaries (user_id);
            """, cancellationToken);
    }

    private async Task<User?> SeedUserAsync(string initialPassword, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users.SingleOrDefaultAsync(user => user.Username == "dominik", cancellationToken);
        if (existingUser is not null)
        {
            return existingUser;
        }

        var user = new User
        {
            Username = "dominik",
            IsAdmin = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialPassword, workFactor: 12)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task SeedCategoriesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingCategories = await _dbContext.Categories
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(category => (category.Name, category.ParentCategoryId), cancellationToken);

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
                UserId = userId,
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
                UserId = userId,
                Name = definition.Name,
                ParentCategoryId = parentCategory.Id,
                SortOrder = definition.SortOrder
            };

            _dbContext.Categories.Add(category);
            existingCategories[(definition.Name, parentCategory.Id)] = category;
        }
    }

    private async Task SeedLlmProvidersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingTypes = await _dbContext.LlmProviders
            .Where(p => p.UserId == userId)
            .Select(provider => provider.ProviderType)
            .ToHashSetAsync(cancellationToken);

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
                UserId = userId,
                ProviderType = provider.Type,
                Name = provider.Name,
                Model = provider.Model,
                IsEnabled = false,
                ApiKeyEncrypted = null
            });
        }
    }

    private async Task SeedSettingsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingKeys = await _dbContext.Settings
            .Where(s => s.UserId == userId)
            .Select(setting => setting.Key)
            .ToHashSetAsync(cancellationToken);

        if (!existingKeys.Contains("sms_webhook_secret"))
        {
            var secret = GenerateUrlSafeSecret();
            _dbContext.Settings.Add(new Setting { Key = "sms_webhook_secret", UserId = userId, Value = secret });
            _logger.LogInformation("Seeded sms_webhook_secret for user {UserId}: {SmsWebhookSecret}", userId, secret);
        }

        if (!existingKeys.Contains("sms_senders"))
        {
            _dbContext.Settings.Add(new Setting
            {
                Key = "sms_senders",
                UserId = userId,
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
