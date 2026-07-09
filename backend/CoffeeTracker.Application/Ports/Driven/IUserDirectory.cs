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
