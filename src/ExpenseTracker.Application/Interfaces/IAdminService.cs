using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IAdminService
{
  Task<List<UserDto>> GetUsersAsync(CancellationToken ct);
  Task<UserDto?> CreateUserAsync(CreateUserRequest request, CancellationToken ct);
  Task<bool> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct);
}
