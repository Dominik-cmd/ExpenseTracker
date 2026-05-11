using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(
  IAdminService adminService,
  ILogger<AdminController> logger) : ApiControllerBase
{
  [HttpGet("users")]
  public async Task<ActionResult<List<UserDto>>> GetUsersAsync(CancellationToken ct)
  {
    var users = await adminService.GetUsersAsync(ct);
    return Ok(users);
  }

  [HttpPost("users")]
  public async Task<ActionResult<UserDto>> CreateUserAsync(
    [FromBody] CreateUserRequest request, CancellationToken ct)
  {
    var user = await adminService.CreateUserAsync(request, ct);
    logger.LogInformation("Admin created user {Username} ({UserId})", request.Username, user!.Id);
    return Created($"/api/admin/users/{user!.Id}", user);
  }

  [HttpDelete("users/{id:guid}")]
  public async Task<IActionResult> DeleteUserAsync(Guid id, CancellationToken ct)
  {
    var userId = GetCurrentUserId();
    if (userId is null)
    {
      return Unauthorized();
    }
    var deleted = await adminService.DeleteUserAsync(id, userId.Value, ct);
    if (!deleted)
    {
      return NotFound();
    }
    logger.LogWarning("Admin {AdminId} deleted user {UserId}", userId.Value, id);
    return NoContent();
  }
}
