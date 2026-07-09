using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using CoffeeTracker.Application.Auth;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// Proves the bearer TokenValidationParameters in Program.cs actually reject forged
// or stale tokens on a protected endpoint. Tokens are minted in-test against the
// factory's known signing key (ApiFactory.JwtKey); the accepted-baseline test pins
// the minting harness to the app's real issuer/audience/key so the 401 cases fail
// for the right reason. Weakening ValidateIssuerSigningKey / ValidateLifetime /
// ValidateIssuer / ValidateAudience in Program.cs makes these tests fail.
public sealed class JwtSecurityTests : IntegrationTest
{
    // Matches the JwtOptions defaults (the test config does not override them).
    private const string Issuer = "coffee-tracker";
    private const string Audience = "coffee-tracker";

    /// <summary>A different-but-valid-length HS256 key an attacker might sign with.</summary>
    private const string WrongKey = "attacker-owned-signing-key-attacker-owned-key-01";

    private static string MintToken(
        string key,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? notBefore = null,
        DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "forged-user"),
                new Claim(AppClaims.Admin, "true"),
            ],
            notBefore: notBefore ?? now.AddMinutes(-1),
            expires: expires ?? now.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Correctly_signed_token_minted_with_the_apps_parameters_is_accepted()
    {
        // Baseline: proves the minting harness matches the app's validation setup,
        // so the rejections below are caused by the single deliberately-broken field.
        var res = await Client.Get("/api/coffees", MintToken(ApiFactory.JwtKey));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_the_wrong_key_is_rejected()
    {
        var res = await Client.Get("/api/coffees", MintToken(WrongKey));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        // Expired 10 minutes ago — well beyond the 1-minute ClockSkew.
        var now = DateTime.UtcNow;
        var expired = MintToken(ApiFactory.JwtKey, notBefore: now.AddMinutes(-20), expires: now.AddMinutes(-10));

        var res = await Client.Get("/api/coffees", expired);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Token_with_the_wrong_issuer_is_rejected()
    {
        var res = await Client.Get("/api/coffees", MintToken(ApiFactory.JwtKey, issuer: "evil-issuer"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Token_with_the_wrong_audience_is_rejected()
    {
        var res = await Client.Get("/api/coffees", MintToken(ApiFactory.JwtKey, audience: "some-other-app"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
