using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.IntegrationTests;

public sealed class AuthTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndRefreshToken()
    {
        var client = Factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(CustomWebApplicationFactory.TestUsername, CustomWebApplicationFactory.TestPassword),
            CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadAsAsync<LoginResponse>(response);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.True(payload.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(CustomWebApplicationFactory.TestUsername, "wrong-password"),
            CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokens()
    {
        var client = Factory.CreateApiClient();
        var login = await Factory.LoginAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(login.RefreshToken),
            CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await ReadAsAsync<LoginResponse>(response);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.Token));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Logout_WithValidToken_ClearsRefreshToken()
    {
        var client = Factory.CreateApiClient();
        var login = await Factory.LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(login.RefreshToken),
            CustomWebApplicationFactory.JsonOptions);
        var refreshHash = await Factory.ExecuteDbContextAsync(db =>
            db.Users.Where(x => x.Username == CustomWebApplicationFactory.TestUsername)
                .Select(x => x.RefreshTokenHash)
                .SingleAsync());

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.Null(refreshHash);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = Factory.CreateApiClient();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
