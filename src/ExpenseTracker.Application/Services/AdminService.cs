using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Services;

public sealed class AdminService(
  IUserRepository userRepository,
  ISeedDataService seedDataService) : IAdminService
{
    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct)
    {
        var users = await userRepository.GetAllAsync(ct);
        return users
          .OrderBy(u => u.CreatedAt)
          .Select(u => u.ToDto())
          .ToList();
    }

    public async Task<UserDto?> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
        {
            throw new InvalidOperationException("Username must be at least 3 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }

        if (await userRepository.ExistsByUsernameAsync(request.Username, ct))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
        await seedDataService.SeedForUserAsync(user.Id, ct);

        return user.ToDto();
    }

    public async Task<bool> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct)
    {
        if (id == currentUserId)
        {
            throw new InvalidOperationException("Cannot delete yourself.");
        }

        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null)
        {
            return false;
        }

        await userRepository.RemoveAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
        return true;
    }
}
