using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Services;

public sealed class AuthService(
  IUserRepository userRepository,
  IJwtService jwtService,
  ILogger<AuthService> logger) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Authentication failed for username {Username}", request.Username);
            return null;
        }

        logger.LogDebug("User {UserId} authenticated successfully", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = HashValue(refreshToken);
        var user = await userRepository.GetByRefreshTokenHashAsync(hash, ct);

        if (user is null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            logger.LogDebug("Refresh token validation failed (expired or not found)");
            return null;
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return;
        }

        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);
        logger.LogDebug("User {UserId} refresh token revoked", userId);
    }

    public async Task<LoginResponse?> ChangePasswordAsync(
      Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return null;
        }

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            logger.LogWarning("Password change failed for user {UserId}: incorrect current password", userId);
            return null;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} password changed successfully", userId);
        return await IssueTokensAsync(user, ct);
    }

    private async Task<LoginResponse> IssueTokensAsync(
      Core.Entities.User user, CancellationToken ct)
    {
        var (token, expiresAt) = jwtService.GenerateToken(user);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenHash = HashValue(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30);
        user.UpdatedAt = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(ct);
        return new LoginResponse(token, refreshToken, expiresAt);
    }

    private static string HashValue(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
