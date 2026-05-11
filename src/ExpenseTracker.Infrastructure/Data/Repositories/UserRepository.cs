using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Data.Repositories;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
  public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
  {
    return await dbContext.Users.FindAsync([id], ct);
  }

  public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct)
  {
    return await dbContext.Users
      .FirstOrDefaultAsync(u => u.Username == username, ct);
  }

  public async Task<User?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct)
  {
    return await dbContext.Users
      .FirstOrDefaultAsync(u => u.RefreshToken == refreshTokenHash, ct);
  }

  public async Task<List<User>> GetAllAsync(CancellationToken ct)
  {
    return await dbContext.Users.ToListAsync(ct);
  }

  public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct)
  {
    return await dbContext.Users.AnyAsync(u => u.Username == username, ct);
  }

  public async Task AddAsync(User user, CancellationToken ct)
  {
    await dbContext.Users.AddAsync(user, ct);
  }

  public Task RemoveAsync(User user, CancellationToken ct)
  {
    dbContext.Users.Remove(user);
    return Task.CompletedTask;
  }

  public async Task SaveChangesAsync(CancellationToken ct)
  {
    await dbContext.SaveChangesAsync(ct);
  }
}
