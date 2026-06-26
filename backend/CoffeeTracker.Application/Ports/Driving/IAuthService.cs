using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving (input) port for authentication. The HTTP controller depends only on
/// this; the implementation (an Identity-backed adapter) lives in Infrastructure,
/// so the API layer never references ASP.NET Identity directly.
/// </summary>
public interface IAuthService
{
    /// <summary>Registers a user (subject to the registration flag and password policy).</summary>
    Task<AuthResult> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    /// <summary>Verifies credentials and issues a token on success.</summary>
    Task<AuthResult> LoginAsync(LoginDto dto, CancellationToken ct = default);
}

/// <summary>Outcome of an auth operation. Expected failures are data, not exceptions.</summary>
public enum AuthStatus
{
    Success,
    RegistrationDisabled,
    DuplicateUser,
    WeakPassword,
    InvalidCredentials,
    LockedOut,
}

/// <summary>
/// Result of register/login. <see cref="Response"/> is non-null only when
/// <see cref="Status"/> is <see cref="AuthStatus.Success"/>; <see cref="Errors"/>
/// carries human-readable detail for validation-style failures (e.g. weak password).
/// </summary>
public sealed record AuthResult(AuthStatus Status, AuthResponseDto? Response, IReadOnlyList<string>? Errors = null)
{
    public static AuthResult Success(AuthResponseDto response) => new(AuthStatus.Success, response);
    public static AuthResult Fail(AuthStatus status, IReadOnlyList<string>? errors = null) => new(status, null, errors);
}
