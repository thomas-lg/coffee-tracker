using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving port for signing in through the external provider. Kept apart from
/// <see cref="IAuthService"/>: the credentials, the failure modes and the account
/// resolution rules have nothing in common with the local flow, and the only thing
/// they share is the session they hand back.
/// </summary>
public interface IExternalSignInService
{
    /// <summary>Exchanges a provider ID token for an app session.</summary>
    Task<ExternalSignInResult> SignInAsync(string idToken, CancellationToken ct = default);
}

public enum ExternalSignInStatus
{
    Success,

    /// <summary>No provider is configured on this instance.</summary>
    NotConfigured,

    /// <summary>The token was not one this instance should believe, for any reason.</summary>
    InvalidToken,

    /// <summary>
    /// The token's email matches an existing account but the provider does not assert
    /// it verified that email, so linking would be an account takeover and creating a
    /// second account would strand the first one's data.
    /// </summary>
    UnverifiedEmailConflict,
}

public sealed record ExternalSignInResult(ExternalSignInStatus Status, AuthResponseDto? Response)
{
    public static ExternalSignInResult Success(AuthResponseDto response) => new(ExternalSignInStatus.Success, response);
    public static ExternalSignInResult Fail(ExternalSignInStatus status) => new(status, null);
}
