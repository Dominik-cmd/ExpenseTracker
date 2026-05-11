using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(
  IAuthService authService,
  ILogger<AuthController> logger) : ApiControllerBase
{
  [HttpPost("login")]
  [EnableRateLimiting("auth-login")]
  public async Task<ActionResult<LoginResponse>> LoginAsync(
    [FromBody] LoginRequest request, CancellationToken ct)
  {
    var result = await authService.LoginAsync(request, ct);
    if (result is null)
    {
      logger.LogWarning("Failed login attempt for user {Username}", request.Username);
      return Unauthorized(new { message = "Invalid username or password." });
    }
    logger.LogInformation("User {Username} logged in successfully", request.Username);
    return Ok(result);
  }

  [HttpPost("refresh")]
  public async Task<ActionResult<LoginResponse>> RefreshAsync(
    [FromBody] RefreshRequest request, CancellationToken ct)
  {
    var result = await authService.RefreshAsync(request.RefreshToken, ct);
    if (result is null)
    {
      logger.LogWarning("Invalid refresh token attempt");
      return Unauthorized(new { message = "Invalid refresh token." });
    }
    logger.LogDebug("Token refreshed successfully");
    return Ok(result);
  }

  [Authorize]
  [HttpPost("logout")]
  public async Task<IActionResult> LogoutAsync(CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    await authService.LogoutAsync(userId.Value, ct);
    logger.LogInformation("User {UserId} logged out", userId.Value);
    return NoContent();
  }

  [Authorize]
  [HttpPost("change-password")]
  public async Task<IActionResult> ChangePasswordAsync(
    [FromBody] ChangePasswordRequest request, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var success = await authService.ChangePasswordAsync(userId.Value, request, ct);
    if (!success)
    {
      logger.LogWarning("Failed password change attempt for user {UserId}", userId.Value);
      return BadRequest(new { message = "Current password is incorrect." });
    }
    logger.LogInformation("User {UserId} changed password successfully", userId.Value);
    return NoContent();
  }
}
