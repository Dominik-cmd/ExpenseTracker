using ExpenseTracker.Core.Entities;

namespace ExpenseTracker.Application.Interfaces;

public interface IJwtService
{
  (string Token, DateTime ExpiresAt) GenerateToken(User user);
}
