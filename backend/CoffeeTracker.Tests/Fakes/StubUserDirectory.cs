using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Tests.Fakes;

/// <summary>
/// A user directory whose every member throws until a test overrides it. The throw is
/// the point: a test that reaches a method it did not think about fails loudly instead
/// of quietly passing on a default.
/// </summary>
public abstract class StubUserDirectory : IUserDirectory
{
    public virtual Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<AuthUser?> FindByIdAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<CreateUserResult> CreateAsync(NewUser user, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<bool> IsLockedOutAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<PasswordCheck> VerifyPasswordAsync(string userId, string password, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual void SpendDecoyVerification(string password) => throw new NotSupportedException();

    public virtual Task<bool> HasAdminWithExternalLoginAsync(string issuer, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<bool> HasOtherAdminAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<AuthUser?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task LinkExternalLoginAsync(string userId, string issuer, string subject, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task RemoveLocalPasswordAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task<CreateUserResult> CreateFromExternalAsync(string issuer, string subject, string email, string displayName, CancellationToken ct = default) => throw new NotSupportedException();

    public virtual Task SetAdminAsync(string userId, bool isAdmin, CancellationToken ct = default) => throw new NotSupportedException();
}
