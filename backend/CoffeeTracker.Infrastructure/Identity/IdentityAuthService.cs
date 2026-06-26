using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Driven adapter implementing the <see cref="IAuthService"/> driving port over
/// ASP.NET Identity. Uses <see cref="UserManager{TUser}"/> only (no SignInManager /
/// cookies): this API authenticates with JWTs, so we just verify the password and
/// drive lockout manually.
/// </summary>
public class IdentityAuthService(
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    TokenService tokenService,
    IOptions<RegistrationOptions> registrationOptions) : IAuthService
{
    // A precomputed hash to verify against when the email is unknown, so a login
    // for a non-existent user spends comparable time to a real password check and
    // doesn't leak account existence through response latency.
    private readonly string _decoyPasswordHash = passwordHasher.HashPassword(new AppUser(), "decoy-for-timing-equalization");

    public async Task<AuthResult> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (!registrationOptions.Value.Enabled)
        {
            return AuthResult.Fail(AuthStatus.RegistrationDisabled);
        }

        // Deliberate bootstrap: the first user on a fresh instance becomes admin.
        var isFirstUser = !await userManager.Users.AnyAsync(ct);

        var user = new AppUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            IsAdmin = isFirstUser,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            // Classify by Identity's error codes so the caller maps to the right
            // status — a duplicate email, a weak password, or some other invalid
            // input (e.g. InvalidUserName/InvalidEmail) are distinct outcomes and
            // must not all masquerade as "weak password".
            if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return AuthResult.Fail(AuthStatus.DuplicateUser, errors);
            }
            if (result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase)))
            {
                return AuthResult.Fail(AuthStatus.WeakPassword, errors);
            }
            return AuthResult.Fail(AuthStatus.InvalidInput, errors);
        }

        return AuthResult.Success(BuildResponse(user));
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        // Same response — and comparable latency — whether the user is unknown or
        // the password is wrong, so we don't reveal which emails are registered.
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new AppUser(), _decoyPasswordHash, dto.Password);
            return AuthResult.Fail(AuthStatus.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return AuthResult.Fail(AuthStatus.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user)
                ? AuthResult.Fail(AuthStatus.LockedOut)
                : AuthResult.Fail(AuthStatus.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return AuthResult.Success(BuildResponse(user));
    }

    private AuthResponseDto BuildResponse(AppUser user)
    {
        var (token, expiresAt) = tokenService.CreateToken(user.Id, user.Email, user.DisplayName, user.IsAdmin);
        return new AuthResponseDto(token, expiresAt, user.Id, user.DisplayName, user.IsAdmin);
    }
}
