using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<LoginResponse?> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(Guid userId, CancellationToken ct);
    Task<LoginResponse?> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
}
