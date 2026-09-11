namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Whether accounts created in the app may sign in, and whether new ones may be
/// registered. Both are persisted and changed at runtime by an administrator, so
/// this is a read/write port rather than bound configuration.
/// </summary>
public interface IAccountPolicy
{
    Task<AccountPolicy> GetAsync(CancellationToken ct = default);

    Task SetAsync(AccountPolicy policy, CancellationToken ct = default);
}

/// <summary>
/// The settings, always read and written together so a caller can never act on a
/// half-stale view of the policy.
/// </summary>
/// <param name="LocalLoginEnabled">Whether app accounts may sign in.</param>
/// <param name="LocalRegistrationEnabled">Whether new app accounts may be created.</param>
/// <param name="RegistrationOpenedForBootstrap">
/// Bookkeeping, not a user-facing setting: registration was opened only so an empty
/// instance could get its first account, and closes itself once that account exists.
/// Registration an administrator turned on deliberately carries this false and is
/// never closed behind their back.
/// </param>
public sealed record AccountPolicy(
    bool LocalLoginEnabled,
    bool LocalRegistrationEnabled,
    bool RegistrationOpenedForBootstrap = false);
