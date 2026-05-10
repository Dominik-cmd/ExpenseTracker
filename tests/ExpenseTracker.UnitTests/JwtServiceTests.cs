using System.IdentityModel.Tokens.Jwt;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

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
  public void GetValidationParameters_ShouldReturnCorrectParameters()
  {
    var service = CreateSubject();

    var parameters = service.GetValidationParameters();

    parameters.ValidateIssuerSigningKey.Should().BeTrue();
    parameters.ValidateIssuer.Should().BeTrue();
    parameters.ValidIssuer.Should().Be(Issuer);
    parameters.ValidateAudience.Should().BeTrue();
    parameters.ValidAudience.Should().Be(Audience);
    parameters.ValidateLifetime.Should().BeTrue();
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
}
