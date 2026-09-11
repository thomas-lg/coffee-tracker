namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// The optional external OpenID Connect provider. Nothing here names a specific
/// identity product: endpoints come from the provider's discovery document, so any
/// compliant provider works and a deployment with no provider leaves the section out
/// entirely.
/// </summary>
public class OidcOptions
{
    public const string SectionName = "Oidc";

    /// <summary>
    /// The provider's base URL, from which <c>/.well-known/openid-configuration</c> is
    /// resolved. Required to enable the feature.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>The client id registered with the provider. Required to enable the feature.</summary>
    public string? ClientId { get; set; }

    /// <summary>Scopes the client requests. `openid` is always implied.</summary>
    public string Scopes { get; set; } = "openid profile email";

    /// <summary>
    /// What to call the provider on the sign-in button — "Authelia", "Keycloak", the
    /// name of a company. Optional: unset, the client falls back to a generic label, so
    /// the app never has to know which product it is talking to.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Claim carrying the administrator assertion (e.g. <c>groups</c>). Leave unset to
    /// fall back to promoting the first user who signs in through the provider.
    /// </summary>
    public string? AdminClaim { get; set; }

    /// <summary>Value <see cref="AdminClaim"/> must carry to grant administrator status.</summary>
    public string? AdminClaimValue { get; set; }

    /// <summary>
    /// Whether the authority is one the signing keys can be fetched from safely. Over
    /// plain http anyone on the path can serve their own JWKS and mint ID tokens the
    /// app would accept, so https is required — except on loopback, where there is no
    /// path to be on and a provider is routinely run without a certificate in
    /// development.
    /// </summary>
    public bool HasSecureAuthority =>
        string.IsNullOrWhiteSpace(Authority)
        || (Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback));

    /// <summary>Whether enough is configured for the feature to exist at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>
    /// Whether an administrator mapping is configured. Both halves are required: a
    /// claim with no value to match would grant admin to anyone carrying the claim.
    /// </summary>
    public bool HasAdminClaimMapping =>
        !string.IsNullOrWhiteSpace(AdminClaim) && !string.IsNullOrWhiteSpace(AdminClaimValue);
}
