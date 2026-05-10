using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Services;

public sealed class AuthService(
  IUserRepository userRepository,
  IJwtService jwtService) : IAuthService
{
  public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
  {
    var user = await userRepository.GetByUsernameAsync(request.Username, ct);
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
      return null;
    }

    return await IssueTokensAsync(user, ct);
  }

  public async Task<LoginResponse?> RefreshAsync(string refreshToken, CancellationToken ct)
  {
    var hash = HashValue(refreshToken);
    var users = await userRepository.GetAllAsync(ct);
    var user = users.FirstOrDefault(u =>
      u.RefreshTokenHash == hash && u.RefreshTokenExpiresAt > DateTime.UtcNow);

    if (user is null)
    {
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
  }

  public async Task<bool> ChangePasswordAsync(
    Guid userId, ChangePasswordRequest request, CancellationToken ct)
  {
    var user = await userRepository.GetByIdAsync(userId, ct);
    if (user is null)
    {
      return false;
    }

    if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
    {
      return false;
    }

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
    user.RefreshTokenHash = null;
    user.RefreshTokenExpiresAt = null;
    user.UpdatedAt = DateTime.UtcNow;
    await userRepository.SaveChangesAsync(ct);
    return true;
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
