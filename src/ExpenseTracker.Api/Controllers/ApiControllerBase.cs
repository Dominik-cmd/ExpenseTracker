using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers
{


[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid? GetCurrentUserId()
    {
        var rawValue = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawValue, out var userId) ? userId : null;
    }
}
}

