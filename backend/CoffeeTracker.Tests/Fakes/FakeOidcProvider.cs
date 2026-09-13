using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CoffeeTracker.Tests.Fakes;

/// <summary>
/// A real, minimal OpenID Connect provider on a loopback port: it serves a discovery
/// document and a JWKS, and mints genuinely RSA-signed ID tokens.
///
/// Real rather than stubbed because the thing under test is <c>OidcTokenValidator</c>,
/// and every check it makes — signature, issuer, audience, lifetime — is only exercised
/// if the token really was signed by a key really published at a really discovered
/// endpoint. Handing the validator a pre-built configuration would leave the validation
/// doing nothing while the tests went green. The Playwright suite takes the same line
/// for the same reason.
/// </summary>
public sealed class FakeOidcProvider : IAsyncDisposable
{
    private const string KeyId = "test-key-1";

    private readonly WebApplication _app;
    private readonly RsaSecurityKey _signingKey;

    private FakeOidcProvider(WebApplication app, RsaSecurityKey signingKey, string authority)
    {
        _app = app;
        _signingKey = signingKey;
        Authority = authority;
    }

    /// <summary>Base URL, and the issuer the discovery document advertises.</summary>
    public string Authority { get; }

    public static async Task<FakeOidcProvider> StartAsync()
    {
        var signingKey = new RsaSecurityKey(RSA.Create(2048)) { KeyId = KeyId };

        var builder = WebApplication.CreateSlimBuilder();
        // Port 0: test classes run in parallel and must not collide on a fixed one.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        // Hand-built rather than JsonWebKeySet: its ToString() returns the type name, not
        // JSON, so serving that silently publishes a key set nobody can parse — and the
        // validator then rejects every token for want of a signing key.
        var rsaParameters = signingKey.Rsa.ExportParameters(includePrivateParameters: false);
        var jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(rsaParameters.Modulus),
                    e = Base64UrlEncoder.Encode(rsaParameters.Exponent),
                },
            },
        });

        // The authority is only known once Kestrel has bound, so the discovery document
        // is built per request rather than captured up front.
        app.MapGet("/.well-known/openid-configuration", (HttpContext http) =>
        {
            var authority = $"{http.Request.Scheme}://{http.Request.Host}";
            return Results.Json(new
            {
                issuer = authority,
                jwks_uri = $"{authority}/jwks",
                authorization_endpoint = $"{authority}/authorize",
                token_endpoint = $"{authority}/token",
                response_types_supported = new[] { "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
            });
        });
        app.MapGet("/jwks", () => Results.Text(jwks, "application/json"));

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First()
            .TrimEnd('/');

        return new FakeOidcProvider(app, signingKey, address);
    }

    /// <summary>
    /// Mints an ID token. Everything a validation check looks at can be overridden, so a
    /// test can bend exactly one of them and watch the token be refused.
    /// </summary>
    public string MintIdToken(
        string audience,
        string subject = "subject-1",
        string? issuer = null,
        IEnumerable<Claim>? claims = null,
        DateTime? expires = null,
        SecurityKey? signWith = null)
    {
        // nbf is derived from exp rather than fixed: a test minting an already-expired
        // token would otherwise produce nbf > exp, which the constructor refuses outright
        // and the validator never gets to see.
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var token = new JwtSecurityToken(
            issuer: issuer ?? Authority,
            audience: audience,
            claims: [.. Subject(subject), .. claims ?? []],
            notBefore: expiresAt.AddMinutes(-10),
            expires: expiresAt,
            signingCredentials: new SigningCredentials(
                signWith ?? _signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>An RSA key this provider never publishes — for signature-failure tests.</summary>
    public static RsaSecurityKey UnpublishedKey() => new(RSA.Create(2048)) { KeyId = KeyId };

    private static Claim[] Subject(string subject) =>
        subject.Length == 0 ? [] : [new Claim("sub", subject)];

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
