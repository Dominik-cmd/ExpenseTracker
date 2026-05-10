namespace ExpenseTracker.Application.Models;

public sealed record UserDto(Guid Id, string Username, bool IsAdmin, DateTime CreatedAt);

public sealed record CreateUserRequest(string Username, string Password, bool IsAdmin = false);
