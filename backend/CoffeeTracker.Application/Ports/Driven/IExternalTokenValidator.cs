namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Validates an ID token issued by the configured provider. Everything a token must
/// satisfy to be believed — signature, issuer, audience, lifetime — is the adapter's
/// business; the use case only ever sees an identity it may trust, or nothing.
/// </summary>
public interface IExternalTokenValidator
{
    /// <summary>
    /// Returns the asserted identity, or null when the token is not one this instance
    /// should believe. Deliberately gives no reason: a caller that could distinguish
    /// "wrong signature" from "expired" would leak that distinction to whoever posted
    /// the token.
    /// </summary>
    Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>
/// An identity as asserted by the provider.
/// </summary>
/// <param name="Issuer">Who asserted it — half of the identity's stable key.</param>
/// <param name="Subject">The provider's stable identifier for the person — the other half.</param>
/// <param name="Nonce">The nonce the token carries, to be matched against one this instance issued.</param>
/// <param name="Email">The asserted email, if any.</param>
/// <param name="EmailVerified">Whether the provider asserts it verified that email.</param>
/// <param name="DisplayName">A human-readable name, if the provider supplies one.</param>
/// <param name="AdminAssertion">
/// The configured claim mapping's verdict on administrator status, or null when no
/// mapping is configured — in which case the caller falls back to its own bootstrap
/// rule. Keeping the verdict here and the decision in the use case leaves claim names
/// in configuration and policy in the application.
/// </param>
public sealed record ExternalIdentity(
    string Issuer,
    string Subject,
    string? Nonce,
    string? Email,
    bool EmailVerified,
    string? DisplayName,
    bool? AdminAssertion);
