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

public sealed class AnthropicCategorizationProvider : ILlmCategorizationProvider, ILlmNarrativeProvider
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnthropicCategorizationProvider> _logger;
    private readonly IDataProtector _protector;
    private readonly IDataProtector _legacyProtector;
    private readonly AsyncPolicy<HttpResponseMessage> _retryPolicy = LlmProviderSupport.CreateRetryPolicy();

    public AnthropicCategorizationProvider(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AnthropicCategorizationProvider> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _protector = dataProtectionProvider.CreateProtector(LlmProviderSupport.ApiKeyProtectionPurpose);
        _legacyProtector = dataProtectionProvider.CreateProtector(LlmProviderSupport.LegacyApiKeyProtectionPurpose);
    }

    public LlmProviderType ProviderType => LlmProviderType.Anthropic;

    public Task<CategorizationResult?> CategorizeAsync(CategorizationRequest request, CancellationToken cancellationToken = default)
        => CategorizeAsyncInternal(null, request, Array.Empty<Category>(), cancellationToken);

    public Task<CategorizationResult?> CategorizeAsync(LlmProvider configuration, CategorizationRequest request, IReadOnlyCollection<Category> availableCategories, CancellationToken cancellationToken = default)
        => CategorizeAsyncInternal(configuration, request, availableCategories, cancellationToken);

    private async Task<CategorizationResult?> CategorizeAsyncInternal(LlmProvider? configuration, CategorizationRequest request, IReadOnlyCollection<Category> availableCategories, CancellationToken cancellationToken)
    {
        configuration ??= await GetConfigurationAsync(cancellationToken);
        var apiKey = LlmProviderSupport.DecryptApiKey(_protector, _legacyProtector, configuration.ApiKeyEncrypted);
        var client = _httpClientFactory.CreateClient(nameof(LlmProviderType.Anthropic));
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Remove("anthropic-version");
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var categories = request.Categories.Count > 0 ? request.Categories : LlmProviderSupport.ToHierarchy(availableCategories);
        var systemPrompt = LlmProviderSupport.BuildSystemPrompt(categories);
        var userPrompt = LlmProviderSupport.BuildUserPrompt(request);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(async token =>
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = JsonContent.Create(new
                    {
                        model = configuration.Model,
                        max_tokens = 256,
                        temperature = 0.1,
                        system = systemPrompt,
                        messages = new object[]
                        {
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new { type = "text", text = userPrompt }
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
                _dbContext, nameof(LlmProviderType.Anthropic), configuration.Model,
                systemPrompt, userPrompt, null, null,
                stopwatch.ElapsedMilliseconds, false, ex.Message, request, cancellationToken);
            throw;
        }

        stopwatch.Stop();
        _logger.LogInformation("Anthropic categorization latency: {LatencyMs}ms", stopwatch.ElapsedMilliseconds);

        var responseRaw = default(string?);
        var categorizationResult = default(CategorizationResult?);
        string? errorMessage = null;
        bool success;

        try
        {
            response.EnsureSuccessStatusCode();
            responseRaw = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseRaw);
            var content = document.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
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
            _dbContext, nameof(LlmProviderType.Anthropic), configuration.Model,
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
        var client = _httpClientFactory.CreateClient(nameof(LlmProviderType.Anthropic));
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Remove("anthropic-version");
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var response = await _retryPolicy.ExecuteAsync(async token =>
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(new
                {
                    model = configuration.Model,
                    system = request.SystemPrompt,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = request.UserPrompt }
                            }
                        }
                    },
                    max_tokens = request.MaxTokens,
                    temperature = 0.3
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
            var content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
            var tokensUsed = root.TryGetProperty("usage", out var usage)
                ? usage.GetProperty("input_tokens").GetInt32() + usage.GetProperty("output_tokens").GetInt32()
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
