using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Core.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseTracker.UnitTests;

public sealed class JwtServiceTests
{
    private const string Secret = "unit-tests-secret-key-with-sufficient-length";
    private const string Issuer = "ExpenseTracker.UnitTests";
    private const string Audience = "ExpenseTracker.UnitTests.Client";

    [Fact]
    public void GenerateToken_ShouldContainExpectedClaims()
    {
        var service = CreateSubject();
        var user = new User { Id = Guid.NewGuid(), Username = "dominik" };

        var (token, expiresAt) = service.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        jwt.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Name).Value.Should().Be(user.Username);
        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().ContainSingle().Which.Should().Be(Audience);
        expiresAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(50));
    }

    [Fact]
    public void ValidateToken_ShouldSucceedForValidToken()
    {
        var service = CreateSubject();
        var user = new User { Id = Guid.NewGuid(), Username = "dominik" };
        var (token, _) = service.GenerateToken(user);

        var principal = service.ValidateToken(token);

        principal.Should().NotBeNull();
        var subjectClaim = principal!.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
        subjectClaim.Should().NotBeNull();
        subjectClaim!.Value.Should().Be(user.Id.ToString());
        principal.Claims.Should().Contain(claim => claim.Value == user.Username);
    }

    [Fact]
    public void ValidateToken_ShouldFailForExpiredToken()
    {
        var service = CreateSubject();
        var expiredToken = CreateExpiredToken();

        var principal = service.ValidateToken(expiredToken);

        principal.Should().BeNull();
    }

    private static JwtService CreateSubject()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience
            })
            .Build();

        return new JwtService(configuration);
    }

    private static string CreateExpiredToken()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Name, "dominik")
            }),
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddMinutes(-5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }
}
