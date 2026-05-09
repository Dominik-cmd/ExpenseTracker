using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.IntegrationTests;

public sealed class WebhookTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ReceiveSms_WithValidSecret_ReturnsAccepted()
    {
        var client = Factory.CreateApiClient();

        var response = await client.SendAsync(CreateRequest(
            CustomWebApplicationFactory.TestWebhookSecret,
            new SmsWebhookRequest(
                "OTP banka",
                "POS NAKUP 22.03.2024 14:35, kartica ***1234, znesek 23,45 EUR, MERCATOR MARIBOR SI. Info: 041123456. OTP banka",
                "2024-03-22T14:35:00Z")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadAsAsync<SmsWebhookResponse>(response);
        var rawMessageCount = await Factory.ExecuteDbContextAsync(db => db.RawMessages.CountAsync());

        Assert.NotNull(payload);
        Assert.Equal("accepted", payload!.Status);
        Assert.Equal(1, rawMessageCount);
    }

    [Fact]
    public async Task ReceiveSms_WithDuplicatePayload_ReturnsDuplicate()
    {
        var client = Factory.CreateApiClient();
        var request = new SmsWebhookRequest(
            "OTP banka",
            "POS NAKUP 22.03.2024 14:35, kartica ***1234, znesek 23,45 EUR, MERCATOR MARIBOR SI. Info: 041123456. OTP banka",
            "2024-03-22T14:35:00Z");

        await client.SendAsync(CreateRequest(CustomWebApplicationFactory.TestWebhookSecret, request));
        var duplicateResponse = await client.SendAsync(CreateRequest(CustomWebApplicationFactory.TestWebhookSecret, request));

        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        var payload = await ReadAsAsync<SmsWebhookResponse>(duplicateResponse);
        var rawMessageCount = await Factory.ExecuteDbContextAsync(db => db.RawMessages.CountAsync());

        Assert.NotNull(payload);
        Assert.Equal("duplicate", payload!.Status);
        Assert.Equal(1, rawMessageCount);
    }

    [Fact]
    public async Task ReceiveSms_WithWrongSecret_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        var response = await client.SendAsync(CreateRequest(
            "wrong-secret",
            new SmsWebhookRequest(
                "OTP banka",
                "POS NAKUP 22.03.2024 14:35, kartica ***1234, znesek 23,45 EUR, MERCATOR MARIBOR SI. Info: 041123456. OTP banka",
                "2024-03-22T14:35:00Z")));

        var rawMessageCount = await Factory.ExecuteDbContextAsync(db => db.RawMessages.CountAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, rawMessageCount);
    }

    private static HttpRequestMessage CreateRequest(string secret, SmsWebhookRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/sms")
        {
            Content = JsonContent.Create(request, options: CustomWebApplicationFactory.JsonOptions)
        };

        message.Headers.Add("X-Webhook-Secret", secret);
        return message;
    }
}
