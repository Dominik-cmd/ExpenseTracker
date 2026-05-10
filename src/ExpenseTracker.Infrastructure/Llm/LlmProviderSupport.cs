using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Records;
using Microsoft.AspNetCore.DataProtection;
using Polly;

namespace ExpenseTracker.Infrastructure;

internal static class LlmProviderSupport
{
    public const string ApiKeyProtectionPurpose = "LlmApiKeys";
    public const string LegacyApiKeyProtectionPurpose = "ExpenseTracker.Api.LlmProviders";

    public static AsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromMilliseconds((500 * Math.Pow(2, retryAttempt - 1)) + Random.Shared.Next(0, 250)));
    }

    public static string DecryptApiKey(IDataProtector primaryProtector, IDataProtector legacyProtector, string? encryptedApiKey)
    {
        if (string.IsNullOrWhiteSpace(encryptedApiKey))
        {
            throw new InvalidOperationException("The selected LLM provider does not have an encrypted API key configured.");
        }

        try
        {
            return primaryProtector.Unprotect(encryptedApiKey);
        }
        catch
        {
            return legacyProtector.Unprotect(encryptedApiKey);
        }
    }

    public static string BuildSystemPrompt(IReadOnlyCollection<CategoryHierarchy> categories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You categorize expense tracker transactions.");
        builder.AppendLine("Return JSON only with this schema: {\"category\":\"string\",\"subcategory\":\"string|null\",\"confidence\":0.0,\"reasoning\":\"string\"}.");
        builder.AppendLine("Choose only from the available categories and subcategories listed below.");
        builder.AppendLine();
        builder.AppendLine("Available categories:");
        builder.Append(BuildCategoryHierarchy(categories));
        return builder.ToString().Trim();
    }

    public static IReadOnlyCollection<CategoryHierarchy> ToHierarchy(IReadOnlyCollection<Category> categories)
    {
        return categories
            .Where(category => category.ParentCategoryId is null)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(category => new CategoryHierarchy(
                category.Name,
                categories
                    .Where(child => child.ParentCategoryId == category.Id)
                    .OrderBy(child => child.SortOrder)
                    .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(child => child.Name)
                    .ToList()))
            .ToList();
    }

    public static string BuildUserPrompt(CategorizationRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Categorize this bank transaction:");
        builder.AppendLine($"Merchant raw: {request.MerchantRaw}");
        builder.AppendLine($"Merchant normalized: {request.MerchantNormalized}");
        builder.AppendLine($"Amount: {request.Amount.ToString(CultureInfo.InvariantCulture)} EUR");
        builder.AppendLine($"Direction: {request.Direction?.ToString() ?? string.Empty}");
        builder.AppendLine($"Transaction type: {request.TransactionType}");
        builder.AppendLine($"Purpose / notes: {request.Notes ?? string.Empty}");
        return builder.ToString().Trim();
    }

    public static CategorizationResult? ParseCategorizationResult(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var json = ExtractJson(responseText);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("category", out var categoryElement))
        {
            return null;
        }

        var category = categoryElement.GetString();
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        string? subcategory = null;
        if (root.TryGetProperty("subcategory", out var subcategoryElement) && subcategoryElement.ValueKind != JsonValueKind.Null)
        {
            subcategory = subcategoryElement.GetString();
        }

        string? reasoning = null;
        if (root.TryGetProperty("reasoning", out var reasoningElement) && reasoningElement.ValueKind != JsonValueKind.Null)
        {
            reasoning = reasoningElement.GetString();
        }

        var confidence = 0d;
        if (root.TryGetProperty("confidence", out var confidenceElement))
        {
            confidence = confidenceElement.ValueKind switch
            {
                JsonValueKind.Number => confidenceElement.GetDouble(),
                JsonValueKind.String when double.TryParse(confidenceElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0d
            };
        }

        return new CategorizationResult(category, subcategory, confidence, reasoning);
    }

    public static async Task WriteCallLogAsync(
        AppDbContext dbContext,
        string providerType,
        string model,
        string systemPrompt,
        string userPrompt,
        string? responseRaw,
        CategorizationResult? result,
        long latencyMs,
        bool success,
        string? errorMessage,
        CategorizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            dbContext.LlmCallLogs.Add(new Core.Entities.LlmCallLog
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ProviderType = providerType,
                Model = model,
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                ResponseRaw = responseRaw,
                Purpose = "categorize",
                ParsedCategory = result?.CategoryName,
                ParsedSubcategory = result?.SubcategoryName,
                ParsedConfidence = result?.Confidence,
                ParsedReasoning = result?.Reasoning,
                LatencyMs = latencyMs,
                Success = success,
                ErrorMessage = errorMessage,
                MerchantRaw = request.MerchantRaw,
                MerchantNormalized = request.MerchantNormalized,
                Amount = request.Amount,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Log writing must never crash the main flow
        }
    }

    private static string BuildCategoryHierarchy(IReadOnlyCollection<CategoryHierarchy> categories)
    {
        var builder = new StringBuilder();
        foreach (var category in categories.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("- ").AppendLine(category.Name);
            foreach (var subcategory in category.Subcategories.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("  - ").AppendLine(subcategory);
            }
        }

        return builder.ToString();
    }

    private static string ExtractJson(string responseText)
    {
        var startIndex = responseText.IndexOf('{');
        var endIndex = responseText.LastIndexOf('}');
        return startIndex >= 0 && endIndex > startIndex
            ? responseText[startIndex..(endIndex + 1)]
            : responseText;
    }
}
