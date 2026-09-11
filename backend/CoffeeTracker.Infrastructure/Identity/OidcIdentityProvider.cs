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
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly ILogger<OidcIdentityProvider> _logger;

    public OidcIdentityProvider(IOptions<OidcOptions> options, ILogger<OidcIdentityProvider> logger)
    {
        var authority = options.Value.Authority!.TrimEnd('/');
        ConfiguredIssuer = authority;
        _logger = logger;
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase) });
    }

    public string? ConfiguredIssuer { get; }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default) =>
        await GetConfigurationAsync(ct) is not null;

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
            // means the same thing to every caller — there is no usable provider right
            // now — and none of them can do anything but wait.
            _logger.LogWarning(ex, "OIDC discovery failed for {Issuer}; the provider is unavailable for now.", ConfiguredIssuer);
            return null;
        }
    }
}
