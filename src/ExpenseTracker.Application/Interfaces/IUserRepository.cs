using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface IUserRepository
{
  Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
  Task<User?> GetByUsernameAsync(string username, CancellationToken ct);
  Task<List<User>> GetAllAsync(CancellationToken ct);
  Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct);
  Task AddAsync(User user, CancellationToken ct);
  Task RemoveAsync(User user, CancellationToken ct);
  Task SaveChangesAsync(CancellationToken ct);
}
