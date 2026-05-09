using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers
{


[Route("api/auth")]
public sealed class AuthController(AppDbContext dbContext, JwtService jwtService, ILogger<AuthController> logger) : ApiControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingMiddleware.LoginPolicyName)]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == request.Username, ct);
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            return Ok(await IssueTokensAsync(user, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed for {Username}.", request.Username);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to complete login.");
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshAsync([FromBody] RefreshRequest request, CancellationToken ct)
    {
        try
        {
            var refreshHash = HashValue(request.RefreshToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshHash, ct);
            if (user is null || user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
            {
                return Unauthorized(new { message = "Invalid refresh token." });
            }

            return Ok(await IssueTokensAsync(user, ct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Refresh token exchange failed.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to refresh token.");
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId.Value, ct);
            if (user is null)
            {
                return NotFound();
            }

            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Logout failed.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to logout.");
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId is null)
            {
                return Unauthorized();
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId.Value, ct);
            if (user is null)
            {
                return NotFound();
            }

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password change failed.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to change password.");
        }
    }

    private async Task<LoginResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenHash = HashValue(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30);
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var (token, expiresAt) = jwtService.GenerateToken(user);
        return new LoginResponse(token, refreshToken, expiresAt);
    }

    private static string HashValue(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
}

