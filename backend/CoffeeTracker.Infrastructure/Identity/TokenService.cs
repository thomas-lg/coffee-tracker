using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoffeeTracker.Application.Auth;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Driven adapter implementing <see cref="ITokenIssuer"/>: signs short-lived JWT
/// access tokens. The user id is emitted as <see cref="ClaimTypes.NameIdentifier"/>
/// (robust against JWT bearer's inbound claim mapping) and admin status as the
/// shared <see cref="AppClaims.Admin"/> claim.
/// </summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenIssuer
{
    private readonly JwtOptions _opts = options.Value;

    public AccessToken CreateAccessToken(AuthUser user)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(AppClaims.Admin, user.IsAdmin ? "true" : "false"),
        };
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim("displayName", user.DisplayName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
