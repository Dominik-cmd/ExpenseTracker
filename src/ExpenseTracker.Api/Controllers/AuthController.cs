using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
  [HttpPost("login")]
  [EnableRateLimiting("auth-login")]
  public async Task<ActionResult<LoginResponse>> LoginAsync(
    [FromBody] LoginRequest request, CancellationToken ct)
  {
    var result = await authService.LoginAsync(request, ct);
    if (result is null)
    {
      return Unauthorized(new { message = "Invalid username or password." });
    }
    return Ok(result);
  }

  [HttpPost("refresh")]
  public async Task<ActionResult<LoginResponse>> RefreshAsync(
    [FromBody] RefreshRequest request, CancellationToken ct)
  {
    var result = await authService.RefreshAsync(request.RefreshToken, ct);
    if (result is null)
    {
      return Unauthorized(new { message = "Invalid refresh token." });
    }
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
      return BadRequest(new { message = "Current password is incorrect." });
    }
    return NoContent();
  }
}
