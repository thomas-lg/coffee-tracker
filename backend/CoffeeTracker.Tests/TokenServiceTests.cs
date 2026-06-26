using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoffeeTracker.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// TokenService is pure (IOptions + TimeProvider), so it's unit-testable without a
// UserManager. These guard the claims/expiry contract that HttpContextCurrentUser
// and the JWT bearer validation both depend on.
public class TokenServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 26, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly JwtOptions Options = new()
    {
        // 48 bytes, well over the HS256 minimum.
        Key = "test-signing-key-test-signing-key-test-signing-key!",
        Issuer = "coffee-tracker",
        Audience = "coffee-tracker",
        LifetimeMinutes = 60,
    };

    private static TokenService NewService() =>
        new(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(FixedNow));

    private static JwtSecurityToken Decode(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void CreateToken_EmitsUserIdAsNameIdentifier()
    {
        var (token, _) = NewService().CreateToken("user-123", "a@x.com", "Alice", isAdmin: false);

        var jwt = Decode(token);
        // JwtSecurityTokenHandler serializes ClaimTypes.NameIdentifier as "nameid".
        var sub = jwt.Claims.Single(c => c.Type is ClaimTypes.NameIdentifier or "nameid").Value;
        Assert.Equal("user-123", sub);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void CreateToken_EmitsIsAdminClaim(bool isAdmin, string expected)
    {
        var (token, _) = NewService().CreateToken("u", "a@x.com", "A", isAdmin);

        var claim = Decode(token).Claims.Single(c => c.Type == TokenService.AdminClaim);
        Assert.Equal(expected, claim.Value);
    }

    [Fact]
    public void CreateToken_SetsExpiryFromLifetimeAndIssuer()
    {
        var (token, expiresAt) = NewService().CreateToken("u", "a@x.com", "A", isAdmin: false);

        Assert.Equal(FixedNow.AddMinutes(Options.LifetimeMinutes), expiresAt);
        var jwt = Decode(token);
        Assert.Equal(Options.Issuer, jwt.Issuer);
        Assert.Contains(Options.Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateToken_OmitsOptionalClaimsWhenAbsent()
    {
        var (token, _) = NewService().CreateToken("u", email: null, displayName: null, isAdmin: false);

        var jwt = Decode(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "displayName");
        Assert.DoesNotContain(jwt.Claims, c => c.Type is ClaimTypes.Email or "email");
    }
}
