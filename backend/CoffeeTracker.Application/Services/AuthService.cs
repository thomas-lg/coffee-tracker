using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Authentication use case. Owns the register/login/refresh flow (including the
/// first-user-admin bootstrap policy and lockout ordering); delegates identity
/// mechanics, token signing, and refresh-token persistence to driven ports. Logs
/// security-relevant events (registrations, failed logins, lockouts) for forensics.
/// </summary>
public sealed class AuthService(
    IUserDirectory users,
    ITokenIssuer tokenIssuer,
    IRefreshTokenStore refreshTokens,
    IRegistrationPolicy registration,
    ILogger<AuthService> logger) : IAuthService
{
    public bool RegistrationEnabled => registration.Enabled;

    public async Task<AuthResult> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (!registration.Enabled)
        {
            return AuthResult.Fail(AuthStatus.RegistrationDisabled);
        }

        // The store grants admin to the first user on a fresh instance (atomic bootstrap).
        var created = await users.CreateAsync(new NewUser(dto.Email, dto.DisplayName, dto.Password), ct);
        if (created.User is null)
        {
            return created.Error switch
            {
                CreateUserError.Duplicate => AuthResult.Fail(AuthStatus.DuplicateUser, created.Messages),
                CreateUserError.WeakPassword => AuthResult.Fail(AuthStatus.WeakPassword, created.Messages),
                _ => AuthResult.Fail(AuthStatus.InvalidInput, created.Messages),
            };
        }

        if (created.User.IsAdmin)
        {
            logger.LogWarning("Admin bootstrap: user {UserId} was granted admin as the first account.", created.User.Id);
        }
        else
        {
            logger.LogInformation("Registered new user {UserId}.", created.User.Id);
        }

        return AuthResult.Success(await BuildResponseAsync(created.User, ct));
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(dto.Email, ct);

        // Same response — and comparable latency — whether the user is unknown or the
        // password is wrong, so we don't reveal which emails are registered.
        if (user is null)
        {
            users.SpendDecoyVerification(dto.Password);
            // Don't log the attacker-supplied email itself (log-forging vector, and an
            // unknown value has little forensic worth) — just that an attempt happened.
            logger.LogWarning("Failed login attempt for an unknown email.");
            return AuthResult.Fail(AuthStatus.InvalidCredentials);
        }

        // Check lockout before the password so a locked account can't be probed.
        if (await users.IsLockedOutAsync(user.Id, ct))
        {
            logger.LogWarning("Login blocked: account {UserId} is locked out.", user.Id);
            return AuthResult.Fail(AuthStatus.LockedOut);
        }

        var check = await users.VerifyPasswordAsync(user.Id, dto.Password, ct);
        switch (check)
        {
            case PasswordCheck.Valid:
                return AuthResult.Success(await BuildResponseAsync(user, ct));
            case PasswordCheck.LockedOut:
                logger.LogWarning("Account {UserId} locked out after repeated failed logins.", user.Id);
                return AuthResult.Fail(AuthStatus.LockedOut);
            default:
                logger.LogWarning("Failed login for account {UserId}.", user.Id);
                return AuthResult.Fail(AuthStatus.InvalidCredentials);
        }
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthResult.Fail(AuthStatus.InvalidRefreshToken);
        }

        var rotation = await refreshTokens.ValidateAndRotateAsync(refreshToken, ct);
        if (!rotation.Succeeded || rotation.UserId is null || rotation.Replacement is null)
        {
            return AuthResult.Fail(AuthStatus.InvalidRefreshToken);
        }

        // Re-read the user so a fresh access token reflects current claims (e.g. a
        // since-revoked admin flag), and so a deleted user can't refresh.
        var user = await users.FindByIdAsync(rotation.UserId, ct);
        if (user is null)
        {
            return AuthResult.Fail(AuthStatus.InvalidRefreshToken);
        }

        var access = tokenIssuer.CreateAccessToken(user);
        return AuthResult.Success(new AuthResponseDto(
            access.Token,
            access.ExpiresAt,
            rotation.Replacement.Token,
            rotation.Replacement.ExpiresAt,
            user.Id,
            user.DisplayName,
            user.IsAdmin));
    }

    public Task LogoutAsync(string refreshToken, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(refreshToken)
            ? Task.CompletedTask
            : refreshTokens.RevokeAsync(refreshToken, ct);

    private async Task<AuthResponseDto> BuildResponseAsync(AuthUser user, CancellationToken ct)
    {
        var access = tokenIssuer.CreateAccessToken(user);
        var refresh = await refreshTokens.IssueAsync(user.Id, ct);
        return new AuthResponseDto(
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            user.Id,
            user.DisplayName,
            user.IsAdmin);
    }
}
