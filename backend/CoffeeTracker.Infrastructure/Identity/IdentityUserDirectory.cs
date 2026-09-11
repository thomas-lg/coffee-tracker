using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>
/// Driven adapter implementing <see cref="IUserDirectory"/> over ASP.NET Identity.
/// Uses <see cref="UserManager{TUser}"/> only (no SignInManager / cookies): this API
/// authenticates with JWTs, so we verify the password and drive lockout manually.
/// </summary>
public sealed class IdentityUserDirectory(
    UserManager<AppUser> userManager,
    AppDbContext db,
    IPasswordHasher<AppUser> passwordHasher) : IUserDirectory
{
    // Precomputed once per process (not per request): a hash to verify against when the
    // email is unknown, so a login for a non-existent user spends comparable time to a
    // real password check and doesn't leak account existence through response latency.
    private static readonly Lazy<string> DecoyHash = new(() =>
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), "decoy-for-timing-equalization"));

    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user is null ? null : Map(user);
    }

    public async Task<AuthUser?> FindByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : Map(user);
    }

    public async Task<CreateUserResult> CreateAsync(NewUser spec, CancellationToken ct = default)
    {
        var user = new AppUser
        {
            UserName = spec.Email,
            Email = spec.Email,
            DisplayName = spec.DisplayName,
            IsAdmin = false,
        };

        var result = await userManager.CreateAsync(user, spec.Password);
        if (!result.Succeeded)
        {
            var messages = result.Errors.Select(e => e.Description).ToList();
            // Classify by Identity's error codes so the caller maps to the right status —
            // a duplicate email, a weak password, or some other invalid input are distinct
            // outcomes and must not all masquerade as "weak password".
            if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return CreateUserResult.Fail(CreateUserError.Duplicate, messages);
            }
            if (result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase)))
            {
                return CreateUserResult.Fail(CreateUserError.WeakPassword, messages);
            }
            return CreateUserResult.Fail(CreateUserError.Invalid, messages);
        }

        // Bootstrap the first user as admin, race-free: SQLite serialises writers, so this
        // conditional UPDATE promotes exactly one user even if two registrations run at once
        // (each reads-and-writes atomically; the second sees the first's admin row and no-ops).
        var promoted = await db.Database.ExecuteSqlRawAsync(
            "UPDATE AspNetUsers SET IsAdmin = 1 " +
            "WHERE Id = {0} AND NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE IsAdmin = 1 AND Id <> {0})",
            [user.Id], ct);

        return CreateUserResult.Ok(Map(user) with { IsAdmin = promoted == 1 });
    }

    public async Task<bool> IsLockedOutAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.IsLockedOutAsync(user);
    }

    public async Task<PasswordCheck> VerifyPasswordAsync(string userId, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return PasswordCheck.Invalid;
        }

        if (await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return PasswordCheck.Valid;
        }

        await userManager.AccessFailedAsync(user);
        return await userManager.IsLockedOutAsync(user) ? PasswordCheck.LockedOut : PasswordCheck.Invalid;
    }

    public void SpendDecoyVerification(string password) =>
        passwordHasher.VerifyHashedPassword(new AppUser(), DecoyHash.Value, password);

    /// <summary>
    /// Joins AspNetUserLogins to AspNetUsers rather than loading users: the guard only
    /// needs to know whether such a pair exists, and this stays one query as the table
    /// grows.
    /// </summary>
    public async Task<bool> HasAdminWithExternalLoginAsync(string issuer, CancellationToken ct = default) =>
        await (from login in db.UserLogins
               join user in db.Users on login.UserId equals user.Id
               where login.LoginProvider == issuer && user.IsAdmin
               select login.UserId)
            .AnyAsync(ct);

    public async Task<bool> HasOtherAdminAsync(string userId, CancellationToken ct = default) =>
        await db.Users.AnyAsync(u => u.IsAdmin && u.Id != userId, ct);

    public async Task<AuthUser?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken ct = default)
    {
        var user = await userManager.FindByLoginAsync(issuer, subject);
        return user is null ? null : Map(user);
    }

    public async Task LinkExternalLoginAsync(string userId, string issuer, string subject, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Cannot link an external login to unknown user {userId}.");

        var result = await userManager.AddLoginAsync(user, new UserLoginInfo(issuer, subject, issuer));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to link the external login: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<CreateUserResult> CreateFromExternalAsync(
        string issuer,
        string subject,
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsAdmin = false,
        };

        // No password: this account's credentials live at the provider, and a null hash
        // means Identity's password check can never succeed for it.
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            var messages = created.Errors.Select(e => e.Description).ToList();
            return created.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                ? CreateUserResult.Fail(CreateUserError.Duplicate, messages)
                : CreateUserResult.Fail(CreateUserError.Invalid, messages);
        }

        var linked = await userManager.AddLoginAsync(user, new UserLoginInfo(issuer, subject, issuer));
        if (!linked.Succeeded)
        {
            // Leaving an account with no way to sign in would be worse than failing:
            // the email is now taken and the next attempt would collide with it.
            await userManager.DeleteAsync(user);
            return CreateUserResult.Fail(
                CreateUserError.Invalid,
                linked.Errors.Select(e => e.Description).ToList());
        }

        // Same atomic bootstrap as a local registration — see CreateAsync.
        var promoted = await db.Database.ExecuteSqlRawAsync(
            "UPDATE AspNetUsers SET IsAdmin = 1 " +
            "WHERE Id = {0} AND NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE IsAdmin = 1 AND Id <> {0})",
            [user.Id], ct);

        return CreateUserResult.Ok(Map(user) with { IsAdmin = promoted == 1 });
    }

    public async Task SetAdminAsync(string userId, bool isAdmin, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"Cannot set admin on unknown user {userId}.");

        user.IsAdmin = isAdmin;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            // Swallowing this would mint an access token whose admin claim contradicts
            // the database — granting rights the store never recorded.
            throw new InvalidOperationException(
                $"Failed to set administrator status on {userId}: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static AuthUser Map(AppUser user) => new(user.Id, user.Email, user.DisplayName, user.IsAdmin);
}
