using System;

namespace ExpenseTracker.Api.Models
{


public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string RefreshToken, DateTime ExpiresAt);

public sealed record RefreshRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}

