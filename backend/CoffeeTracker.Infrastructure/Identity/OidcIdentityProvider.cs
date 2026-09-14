using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// The configured provider, resolved from its discovery document.
///
/// Discovery is lazy and cached by <see cref="ConfigurationManager{T}"/> (which also
/// handles periodic refresh and key rollover), so a provider that is slow or down at
/// startup costs nothing: the app boots, reports itself provider-less, and starts
/// offering the sign-in as soon as discovery succeeds.
/// </summary>
public sealed class OidcIdentityProvider : IExternalIdentityProvider
{
    private readonly OidcOptions _options;
    private readonly string _authority;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly ILogger<OidcIdentityProvider> _logger;

    public OidcIdentityProvider(IOptions<OidcOptions> options, ILogger<OidcIdentityProvider> logger)
    {
        _options = options.Value;
        var authority = (options.Value.Authority
            ?? throw new InvalidOperationException(
                $"{nameof(OidcIdentityProvider)} was constructed without an authority. It must only be resolved " +
                "when the provider is configured; see AddExternalIdentityProvider."))
            .TrimEnd('/');
        _authority = authority;
        _logger = logger;
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase) });
    }

    /// <summary>
    /// The key external logins are recorded under, the configured authority, not the
    /// discovery document's issuer. The two are allowed to differ (a trailing slash, a
    /// path prefix), so the validator stamps this same value rather than the token's
    /// issuer: the lock-out guard compares them, and deriving one from discovery would
    /// mean a blocking network call behind a property.
    ///
    /// The token's own `iss` is still validated against the discovery issuer, this is
    /// a local namespace for identities, not a trust decision.
    /// </summary>
    public string? ConfiguredIssuer => _authority;

    public async Task<OidcClientConfigDto?> GetClientInfoAsync(CancellationToken ct = default) =>
        await GetConfigurationAsync(ct) is not null
            ? new OidcClientConfigDto(ConfiguredIssuer!, _options.ClientId!, _options.Scopes, _options.DisplayName)
            : null;

    /// <summary>
    /// The provider's resolved metadata, or null when discovery has not succeeded.
    /// Infrastructure-only (the Microsoft type never crosses the port), and the one
    /// place discovery failures are swallowed into "no provider".
    /// </summary>
    public async Task<OpenIdConnectConfiguration?> GetConfigurationAsync(CancellationToken ct = default)
    {
        try
        {
            return await _configuration.GetConfigurationAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately broad: any failure to reach or parse the discovery document
            // means the same thing to every caller, there is no usable provider right
            // now, and none of them can do anything but wait.
            _logger.LogWarning(ex, "OIDC discovery failed for {Issuer}; the provider is unavailable for now.", ConfiguredIssuer);
            return null;
        }
    }
}
