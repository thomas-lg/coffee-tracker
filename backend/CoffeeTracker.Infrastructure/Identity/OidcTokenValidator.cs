using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Validates a provider ID token against the discovered signing keys.
///
/// Every check here is load-bearing. Without audience validation, a token minted for
/// any other client of the same provider would sign someone in; without issuer
/// validation, any provider would do; without the signature, anything would.
/// </summary>
public sealed class OidcTokenValidator(
    OidcIdentityProvider provider,
    IOptions<OidcOptions> options,
    ILogger<OidcTokenValidator> logger) : IExternalTokenValidator
{
    private readonly OidcOptions _options = options.Value;

    public async Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var configuration = await provider.GetConfigurationAsync(ct);
        if (configuration is null)
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            // The provider's clock is not ours; a minute covers ordinary drift without
            // meaningfully extending a short-lived token's life.
            ClockSkew = TimeSpan.FromMinutes(1),
            // Keep claim types verbatim: the admin claim an operator configures is the
            // one the provider emits, not a .NET-mapped alias of it.
            NameClaimType = "sub",
        };

        ClaimsPrincipal principal;
        SecurityToken validated;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(idToken, parameters, out validated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Every validation failure means the same thing to the caller, do not
            // believe this token, and saying which check failed would tell whoever
            // posted it how to get closer.
            logger.LogWarning(ex, "Rejected a provider ID token.");
            return null;
        }

        var subject = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            logger.LogWarning("Rejected a provider ID token with no subject claim.");
            return null;
        }

        return new ExternalIdentity(
            // The authority, matching what the guard looks logins up by, see
            // OidcIdentityProvider.ConfiguredIssuer. The token's own issuer was just
            // validated against the discovery document above.
            Issuer: provider.ConfiguredIssuer!,
            Subject: subject,
            // The jti when the provider mints one, otherwise a hash of the token itself.
            // Hashing rather than storing the token keeps a spent-token set from being a
            // set of usable credentials.
            TokenId: principal.FindFirstValue("jti") ?? Sha256(idToken),
            ExpiresAt: validated.ValidTo == default
                ? DateTimeOffset.UtcNow
                : new DateTimeOffset(DateTime.SpecifyKind(validated.ValidTo, DateTimeKind.Utc)),
            Email: principal.FindFirstValue("email"),
            EmailVerified: string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            DisplayName: principal.FindFirstValue("name") ?? principal.FindFirstValue("preferred_username"),
            AdminAssertion: ResolveAdminAssertion(principal));
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// The configured claim mapping's verdict, or null when no mapping is configured,
    /// which is what tells the use case to fall back to its own bootstrap rule rather
    /// than read "not an admin" into the silence.
    /// </summary>
    private bool? ResolveAdminAssertion(ClaimsPrincipal principal)
    {
        if (!_options.HasAdminClaimMapping)
        {
            return null;
        }

        // A claim may legitimately repeat (groups, roles), so look at every occurrence.
        return principal.FindAll(_options.AdminClaim!)
            .Any(c => string.Equals(c.Value, _options.AdminClaimValue, StringComparison.Ordinal));
    }
}
