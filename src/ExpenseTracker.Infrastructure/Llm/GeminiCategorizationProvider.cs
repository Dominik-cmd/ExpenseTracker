using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using ExpenseTracker.Core.Interfaces;
using ExpenseTracker.Core.Records;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;

namespace ExpenseTracker.Infrastructure;

public sealed class GeminiCategorizationProvider : ILlmCategorizationProvider, ILlmNarrativeProvider
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeminiCategorizationProvider> _logger;
    private readonly IDataProtector _protector;
    private readonly IDataProtector _legacyProtector;
    private readonly AsyncPolicy<HttpResponseMessage> _retryPolicy = LlmProviderSupport.CreateRetryPolicy();

    public GeminiCategorizationProvider(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GeminiCategorizationProvider> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector(LlmProviderSupport.ApiKeyProtectionPurpose);
        _legacyProtector = dataProtectionProvider.CreateProtector(LlmProviderSupport.LegacyApiKeyProtectionPurpose);
    }

    public LlmProviderType ProviderType => LlmProviderType.Gemini;

    public Task<CategorizationResult?> CategorizeAsync(CategorizationRequest request, CancellationToken cancellationToken = default)
        => CategorizeAsyncInternal(null, request, Array.Empty<Category>(), cancellationToken);

    public Task<CategorizationResult?> CategorizeAsync(LlmProvider configuration, CategorizationRequest request, IReadOnlyCollection<Category> availableCategories, CancellationToken cancellationToken = default)
        => CategorizeAsyncInternal(configuration, request, availableCategories, cancellationToken);

    private async Task<CategorizationResult?> CategorizeAsyncInternal(LlmProvider? configuration, CategorizationRequest request, IReadOnlyCollection<Category> availableCategories, CancellationToken cancellationToken)
    {
        configuration ??= await GetConfigurationAsync(cancellationToken);
        var apiKey = LlmProviderSupport.DecryptApiKey(_protector, _legacyProtector, configuration.ApiKeyEncrypted);
        var client = _httpClientFactory.CreateClient(nameof(LlmProviderType.Gemini));
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(configuration.Model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var categories = request.Categories.Count > 0 ? request.Categories : LlmProviderSupport.ToHierarchy(availableCategories);
        var prompt = $"{LlmProviderSupport.BuildSystemPrompt(categories)}{Environment.NewLine}{Environment.NewLine}{LlmProviderSupport.BuildUserPrompt(request)}";
        var systemPrompt = LlmProviderSupport.BuildSystemPrompt(categories);
        var userPrompt = LlmProviderSupport.BuildUserPrompt(request);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(async token =>
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(new
                    {
                        generationConfig = new
                        {
                            temperature = 0.1,
                            responseMimeType = "application/json"
                        },
                        contents = new object[]
                        {
                            new
                            {
                                role = "user",
                                parts = new object[]
                                {
                                    new { text = prompt }
                                }
                            }
                        }
                    })
                };

                return await client.SendAsync(requestMessage, token);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await LlmProviderSupport.WriteCallLogAsync(
                _dbContext, nameof(LlmProviderType.Gemini), configuration.Model,
                systemPrompt, userPrompt, null, null,
                stopwatch.ElapsedMilliseconds, false, ex.Message, request, cancellationToken);
            throw;
        }

        stopwatch.Stop();
        _logger.LogInformation("Gemini categorization latency: {LatencyMs}ms", stopwatch.ElapsedMilliseconds);

        var responseRaw = default(string?);
        var categorizationResult = default(CategorizationResult?);
        string? errorMessage = null;
        bool success;

        try
        {
            response.EnsureSuccessStatusCode();
            responseRaw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseRaw);
            var content = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            categorizationResult = LlmProviderSupport.ParseCategorizationResult(content);
            success = categorizationResult is not null;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            success = false;
        }
        finally
        {
            response.Dispose();
        }

        await LlmProviderSupport.WriteCallLogAsync(
            _dbContext, nameof(LlmProviderType.Gemini), configuration.Model,
            systemPrompt, userPrompt, responseRaw, categorizationResult,
            stopwatch.ElapsedMilliseconds, success, errorMessage, request, cancellationToken);

        return categorizationResult;
    }

    public Task<NarrativeResult> GenerateAsync(NarrativeRequest request, CancellationToken cancellationToken = default)
        => GenerateAsyncInternal(null, request, cancellationToken);

    public Task<NarrativeResult> GenerateAsync(LlmProvider configuration, NarrativeRequest request, CancellationToken cancellationToken = default)
        => GenerateAsyncInternal(configuration, request, cancellationToken);

    private async Task<NarrativeResult> GenerateAsyncInternal(LlmProvider? configuration, NarrativeRequest request, CancellationToken cancellationToken)
    {
        configuration ??= await GetConfigurationAsync(cancellationToken);
        var apiKey = LlmProviderSupport.DecryptApiKey(_protector, _legacyProtector, configuration.ApiKeyEncrypted);
        var client = _httpClientFactory.CreateClient(nameof(LlmProviderType.Gemini));
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(configuration.Model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var response = await _retryPolicy.ExecuteAsync(async token =>
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    system_instruction = new
                    {
                        parts = new object[]
                        {
                            new { text = request.SystemPrompt }
                        }
                    },
                    contents = new object[]
                    {
                        new
                        {
                            role = "user",
                            parts = new object[]
                            {
                                new { text = request.UserPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = request.MaxTokens,
                        temperature = 0.3
                    }
                })
            };

            return await client.SendAsync(requestMessage, token);
        }, cancellationToken);
        try
        {
            response.EnsureSuccessStatusCode();
            var responseRaw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseRaw);
            var root = document.RootElement;
            var content = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? string.Empty;
            var tokensUsed = root.TryGetProperty("usageMetadata", out var usage) && usage.TryGetProperty("totalTokenCount", out var totalTokenCount)
                ? totalTokenCount.GetInt32()
                : (int?)null;

            return new NarrativeResult(content.Trim(), tokensUsed, configuration.Model);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<LlmProvider> GetConfigurationAsync(CancellationToken cancellationToken)
        => await _dbContext.LlmProviders.AsNoTracking().FirstAsync(provider => provider.ProviderType == ProviderType, cancellationToken);
}
