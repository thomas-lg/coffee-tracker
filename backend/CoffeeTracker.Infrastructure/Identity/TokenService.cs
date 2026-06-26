using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Issues signed JWTs for authenticated users. The user id is emitted as
/// <see cref="ClaimTypes.NameIdentifier"/> (robust against JWT bearer's inbound
/// claim mapping) and admin status as a custom <c>isAdmin</c> claim.
/// </summary>
public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public const string AdminClaim = "isAdmin";

    private readonly JwtOptions _opts = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(string userId, string? email, string? displayName, bool isAdmin)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_opts.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(AdminClaim, isAdmin ? "true" : "false"),
        };
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        if (!string.IsNullOrEmpty(displayName))
        {
            claims.Add(new Claim("displayName", displayName));
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

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
