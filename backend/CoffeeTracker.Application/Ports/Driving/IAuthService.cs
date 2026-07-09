using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for authentication. The HTTP controller depends only on
/// this; the implementation (an Application use case over driven ports) issues
/// short-lived access tokens plus rotating refresh tokens, so the API layer never
/// references ASP.NET Identity directly.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Whether open registration is currently allowed. Exposed so an anonymous
    /// client (the login page) can show/hide the register option before signing in.
    /// </summary>
    bool RegistrationEnabled { get; }

    /// <summary>Registers a user (subject to the registration flag and password policy).</summary>
    Task<AuthResult> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    /// <summary>Verifies credentials and issues an access + refresh token pair on success.</summary>
    Task<AuthResult> LoginAsync(LoginDto dto, CancellationToken ct = default);

    /// <summary>Rotates a valid refresh token and issues a fresh access + refresh pair.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes the presented refresh token (sign-out). Idempotent.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}

/// <summary>Outcome of an auth operation. Expected failures are data, not exceptions.</summary>
public enum AuthStatus
{
    Success,
    RegistrationDisabled,
    DuplicateUser,
    WeakPassword,
    /// <summary>Registration rejected for a reason other than duplicate or weak password (e.g. invalid email/username).</summary>
    InvalidInput,
    InvalidCredentials,
    LockedOut,
    InvalidRefreshToken,
}

/// <summary>
/// Result of register/login/refresh. <see cref="Response"/> is non-null only when
/// <see cref="Status"/> is <see cref="AuthStatus.Success"/>; <see cref="Errors"/>
/// carries human-readable detail for validation-style failures (e.g. weak password).
/// </summary>
public sealed record AuthResult(AuthStatus Status, AuthResponseDto? Response, IReadOnlyList<string>? Errors = null)
{
    public static AuthResult Success(AuthResponseDto response) => new(AuthStatus.Success, response);
    public static AuthResult Fail(AuthStatus status, IReadOnlyList<string>? errors = null) => new(status, null, errors);
}
