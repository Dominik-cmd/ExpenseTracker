using ExpenseTracker.Api.Models;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Controllers;

[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(AppDbContext dbContext, SeedDataService seedDataService, ILogger<AdminController> logger) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> GetUsersAsync(CancellationToken ct)
    {
        var users = await dbContext.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserDto(u.Id, u.Username, u.IsAdmin, u.CreatedAt))
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserDto>> CreateUserAsync([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                return BadRequest(new { message = "Username must be at least 3 characters." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return BadRequest(new { message = "Password must be at least 6 characters." });

            if (await dbContext.Users.AnyAsync(u => u.Username == request.Username, ct))
                return Conflict(new { message = "Username is already taken." });

            var user = new User
            {
                Username = request.Username.Trim(),
                IsAdmin = request.IsAdmin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12)
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);

            await seedDataService.SeedForUserAsync(user, ct);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("Admin created user {Username} (id={UserId}).", user.Username, user.Id);
            return Created($"/api/admin/users/{user.Id}", new UserDto(user.Id, user.Username, user.IsAdmin, user.CreatedAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create user {Username}.", request.Username);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to create user.");
        }
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUserAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == id)
                return BadRequest(new { message = "You cannot delete your own account." });

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
            if (user is null) return NotFound();

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("Admin deleted user {Username} (id={UserId}).", user.Username, id);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete user {UserId}.", id);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unable to delete user.");
        }
    }
}

public sealed record UserDto(Guid Id, string Username, bool IsAdmin, DateTime CreatedAt);

public sealed record CreateUserRequest(string Username, string Password, bool IsAdmin = false);
