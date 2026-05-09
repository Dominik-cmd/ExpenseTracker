using System.Text.Json;
using System.Threading.Channels;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Core.Records;
using ExpenseTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services
{


public sealed class SmsProcessingBackgroundService(
    IServiceProvider serviceProvider,
    Channel<Guid> channel,
    ILogger<SmsProcessingBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingMessagesAsync(stoppingToken);

        await foreach (var messageId in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(messageId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected SMS processing error for message {MessageId}.", messageId);
            }
        }
    }

    private async Task RequeuePendingMessagesAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingIds = await dbContext.RawMessages
            .AsNoTracking()
            .Where(x => x.ParseStatus == ParseStatus.Pending)
            .Select(x => x.Id)
            .ToListAsync(ct);

        foreach (var id in pendingIds)
        {
            await channel.Writer.WriteAsync(id, ct);
        }
    }

    private async Task ProcessMessageAsync(Guid messageId, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parser = scope.ServiceProvider.GetRequiredService<OtpBankaSmsParser>();
        var providerResolver = scope.ServiceProvider.GetRequiredService<ILlmProviderResolver>();

        var rawMessage = await dbContext.RawMessages
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == messageId, ct);

        if (rawMessage is null || rawMessage.ParseStatus != ParseStatus.Pending)
        {
            return;
        }

        if (rawMessage.Transactions.Any())
        {
            rawMessage.ParseStatus = ParseStatus.Parsed;
            rawMessage.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var parsed = parser.Parse(rawMessage.Body);
            if (parsed is null)
            {
                rawMessage.ParseStatus = ParseStatus.Failed;
                rawMessage.FailureReason = "Unable to parse SMS payload.";
                rawMessage.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            var userId = rawMessage.UserId;

            var resolution = await ResolveCategoryAsync(dbContext, providerResolver, parsed, userId, ct);
            var transaction = new Transaction
            {
                UserId = userId,
                Amount = parsed.Amount,
                Currency = "EUR",
                Direction = parsed.Direction,
                TransactionType = parsed.TransactionType,
                TransactionDate = parsed.TransactionDate,
                MerchantRaw = parsed.MerchantRaw,
                MerchantNormalized = parsed.MerchantNormalized,
                CategoryId = resolution.Category.Id,
                CategorySource = resolution.Source,
                TransactionSource = TransactionSource.Sms,
                Notes = parsed.Notes,
                RawMessageId = rawMessage.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Transactions.Add(transaction);
            rawMessage.ParseStatus = ParseStatus.Parsed;
            rawMessage.FailureReason = null;
            rawMessage.UpdatedAt = DateTime.UtcNow;

            if (resolution.CreateRule && !string.IsNullOrWhiteSpace(parsed.MerchantNormalized))
            {
                await UpsertMerchantRuleAsync(dbContext, parsed.MerchantNormalized, resolution.Category.Id, "llm", userId, ct);
            }

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            rawMessage.ParseStatus = ParseStatus.Failed;
            rawMessage.FailureReason = ex.Message;
            rawMessage.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            logger.LogError(ex, "Failed to process raw message {MessageId}.", messageId);
        }
    }

    private static async Task<CategoryResolution> ResolveCategoryAsync(AppDbContext dbContext, ILlmProviderResolver providerResolver, ParsedSms parsed, Guid userId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(parsed.MerchantNormalized))
        {
            var rule = await dbContext.MerchantRules
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MerchantNormalized == parsed.MerchantNormalized, ct);

            if (rule is not null)
            {
                rule.HitCount += 1;
                rule.LastHitAt = DateTime.UtcNow;
                rule.UpdatedAt = DateTime.UtcNow;
                return new CategoryResolution(rule.Category, CategorySource.Rule, false);
            }
        }

        var providerConfiguration = await providerResolver.GetEnabledProviderAsync(userId, ct);
        var provider = await providerResolver.ResolveAsync(userId, ct);
        if (provider is not null && providerConfiguration is not null)
        {
            var categories = await dbContext.Categories.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(ct);
            var result = await provider.CategorizeAsync(
                providerConfiguration,
                new CategorizationRequest(parsed.MerchantRaw, parsed.MerchantNormalized, parsed.Amount, parsed.Direction, parsed.TransactionType, parsed.Notes) { UserId = userId },
                categories,
                ct);

            if (result is not null)
            {
                var category = await ResolveCategoryResultAsync(dbContext, result, userId, ct);
                if (category is not null)
                {
                    return new CategoryResolution(category, CategorySource.Llm, !string.Equals(category.Name, "Uncategorized", StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        var fallback = await dbContext.Categories.FirstAsync(x => x.UserId == userId && x.Name == "Uncategorized" && x.ParentCategoryId == null, ct);
        return new CategoryResolution(fallback, CategorySource.Default, false);
    }

    private static async Task<Category?> ResolveCategoryResultAsync(AppDbContext dbContext, CategorizationResult result, Guid userId, CancellationToken ct)
    {
        var parent = await dbContext.Categories.FirstOrDefaultAsync(x => x.UserId == userId && x.Name == result.Category && x.ParentCategoryId == null, ct);
        if (parent is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(result.Subcategory))
        {
            return parent;
        }

        var child = await dbContext.Categories.FirstOrDefaultAsync(x => x.UserId == userId && x.ParentCategoryId == parent.Id && x.Name == result.Subcategory, ct);
        if (child is not null)
        {
            return child;
        }

        child = new Category
        {
            UserId = userId,
            Name = result.Subcategory,
            ParentCategoryId = parent.Id,
            SortOrder = parent.SortOrder,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Categories.Add(child);
        await dbContext.SaveChangesAsync(ct);
        return child;
    }

    private static async Task UpsertMerchantRuleAsync(AppDbContext dbContext, string merchantNormalized, Guid categoryId, string createdBy, Guid userId, CancellationToken ct)
    {
        var rule = await dbContext.MerchantRules.FirstOrDefaultAsync(x => x.UserId == userId && x.MerchantNormalized == merchantNormalized, ct);
        if (rule is null)
        {
            dbContext.MerchantRules.Add(new MerchantRule
            {
                UserId = userId,
                MerchantNormalized = merchantNormalized,
                CategoryId = categoryId,
                CreatedBy = createdBy,
                HitCount = 0,
                LastHitAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        rule.CategoryId = categoryId;
        rule.LastHitAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;
    }

    private sealed record CategoryResolution(Category Category, CategorySource Source, bool CreateRule);
}
}

