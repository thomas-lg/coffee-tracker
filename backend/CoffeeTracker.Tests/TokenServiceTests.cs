using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoffeeTracker.Application.Auth;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Identity;
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
        AccessTokenMinutes = 15,
    };

    private static TokenService NewService() =>
        new(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(FixedNow));

    private static JwtSecurityToken Decode(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static AuthUser User(
        string id = "u",
        string? email = "a@x.com",
        string? displayName = "A",
        bool isAdmin = false) => new(id, email, displayName, isAdmin);

    [Fact]
    public void CreateAccessToken_EmitsUserIdAsNameIdentifier()
    {
        var access = NewService().CreateAccessToken(User(id: "user-123", displayName: "Alice"));

        var jwt = Decode(access.Token);
        // JwtSecurityTokenHandler serializes ClaimTypes.NameIdentifier as "nameid".
        var sub = jwt.Claims.Single(c => c.Type is ClaimTypes.NameIdentifier or "nameid").Value;
        Assert.Equal("user-123", sub);
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void CreateAccessToken_EmitsIsAdminClaim(bool isAdmin, string expected)
    {
        var access = NewService().CreateAccessToken(User(isAdmin: isAdmin));

        var claim = Decode(access.Token).Claims.Single(c => c.Type == AppClaims.Admin);
        Assert.Equal(expected, claim.Value);
    }

    [Fact]
    public void CreateAccessToken_SetsExpiryFromLifetimeAndIssuer()
    {
        var access = NewService().CreateAccessToken(User());

        Assert.Equal(FixedNow.AddMinutes(Options.AccessTokenMinutes), access.ExpiresAt);
        var jwt = Decode(access.Token);
        Assert.Equal(Options.Issuer, jwt.Issuer);
        Assert.Contains(Options.Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateAccessToken_OmitsOptionalClaimsWhenAbsent()
    {
        var access = NewService().CreateAccessToken(User(email: null, displayName: null));

        var jwt = Decode(access.Token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "displayName");
        Assert.DoesNotContain(jwt.Claims, c => c.Type is ClaimTypes.Email or "email");
    }
}
