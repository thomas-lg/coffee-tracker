namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Abstracts the user/identity store so the auth use case can orchestrate
/// registration and login without depending on ASP.NET Identity. The adapter keeps
/// the identity mechanics (hashing, lockout counters); the use case owns the flow.
/// </summary>
public interface IUserDirectory
{
    Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<AuthUser?> FindByIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a user. The store atomically grants admin to the first user on a fresh
    /// instance (bootstrap); the returned user's <see cref="AuthUser.IsAdmin"/> reflects
    /// the outcome, even under concurrent registrations.
    /// </summary>
    Task<CreateUserResult> CreateAsync(NewUser user, CancellationToken ct = default);

    Task<bool> IsLockedOutAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Verifies a password. On failure, records a failed access attempt so account
    /// lockout can trip; on success, resets the failed-attempt counter.
    /// </summary>
    Task<PasswordCheck> VerifyPasswordAsync(string userId, string password, CancellationToken ct = default);

    /// <summary>
    /// Spends CPU comparable to a real password verification, so a login for an unknown
    /// email doesn't finish measurably faster (account-enumeration defence).
    /// </summary>
    void SpendDecoyVerification(string password);

    /// <summary>
    /// Whether any administrator has an external identity recorded for
    /// <paramref name="issuer"/> — i.e. has actually signed in through the provider.
    /// The lock-out guard turns on this: it is the proof that disabling local sign-in
    /// leaves a door open.
    /// </summary>
    Task<bool> HasAdminWithExternalLoginAsync(string issuer, CancellationToken ct = default);

    /// <summary>
    /// Whether an administrator other than <paramref name="userId"/> exists. The
    /// provider's claim mapping revokes as well as grants, and an instance with no
    /// administrator left cannot appoint one from inside the app.
    /// </summary>
    Task<bool> HasOtherAdminAsync(string userId, CancellationToken ct = default);

    /// <summary>Finds the account an external identity is already attached to, if any.</summary>
    Task<AuthUser?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken ct = default);

    /// <summary>Attaches an external identity to an existing account.</summary>
    Task LinkExternalLoginAsync(string userId, string issuer, string subject, CancellationToken ct = default);

    /// <summary>
    /// Drops the account's local password, if it has one, leaving the external identity
    /// as its only credential — the same shape <see cref="CreateFromExternalAsync"/>
    /// produces. Idempotent: an account with no password is already in the target state.
    /// </summary>
    Task RemoveLocalPasswordAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Creates an account for an external identity and attaches it, with no password:
    /// such an account has no local credentials to guess. Grants admin to the first
    /// user on a fresh instance, exactly as <see cref="CreateAsync"/> does.
    /// </summary>
    Task<CreateUserResult> CreateFromExternalAsync(
        string issuer,
        string subject,
        string email,
        string displayName,
        CancellationToken ct = default);

    /// <summary>
    /// Sets administrator status. Used when a provider claim is the source of truth, so
    /// revoking a group there takes effect at the user's next sign-in.
    /// </summary>
    Task SetAdminAsync(string userId, bool isAdmin, CancellationToken ct = default);
}

/// <summary>A user as the application layer sees it (no framework types).</summary>
public sealed record AuthUser(string Id, string? Email, string? DisplayName, bool IsAdmin);

/// <summary>Details for a new registration.</summary>
public sealed record NewUser(string Email, string DisplayName, string Password);

public enum CreateUserError
{
    None,
    Duplicate,
    WeakPassword,
    Invalid,
}

public sealed record CreateUserResult(AuthUser? User, CreateUserError Error, IReadOnlyList<string>? Messages = null)
{
    public static CreateUserResult Ok(AuthUser user) => new(user, CreateUserError.None);
    public static CreateUserResult Fail(CreateUserError error, IReadOnlyList<string>? messages = null) => new(null, error, messages);
}

public enum PasswordCheck
{
    Valid,
    Invalid,
    LockedOut,
}
