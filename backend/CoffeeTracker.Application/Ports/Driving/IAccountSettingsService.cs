using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driving;

/// <summary>
/// Driving port for the administrator's view of the account policy. Separate from
/// <see cref="IAuthService"/> because this is administration, not authentication:
/// reading and changing who may get in, rather than letting them in.
/// </summary>
public interface IAccountSettingsService
{
    Task<AccountSettingsDto> GetAsync(CancellationToken ct = default);

    Task<AccountSettingsUpdate> UpdateAsync(AccountSettings settings, CancellationToken ct = default);
}

public enum AccountSettingsStatus
{
    Applied,

    /// <summary>
    /// Disabling local sign-in was refused because no administrator has signed in
    /// through the provider, so it would have been the last way in.
    /// </summary>
    WouldLockEveryoneOut,
}

/// <summary>
/// Outcome of an update. <see cref="Settings"/> is what is now stored — unchanged
/// when the update was refused, so a client can re-render from it either way.
/// </summary>
public sealed record AccountSettingsUpdate(
    AccountSettingsStatus Status,
    AccountSettingsDto Settings,
    string? Reason = null);

/// <summary>The same two flags, once validation has established both are present.</summary>
public sealed record AccountSettings(bool LocalLoginEnabled, bool LocalRegistrationEnabled);
