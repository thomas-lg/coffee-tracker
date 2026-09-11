using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Administration of the account policy. Owns the one rule that makes it more than a
/// setter: local sign-in may not be switched off while it is the only way into the
/// instance.
/// </summary>
public sealed class AccountSettingsService(
    IAccountPolicy accountPolicy,
    IUserDirectory users,
    IExternalIdentityProvider provider,
    ILogger<AccountSettingsService> logger) : IAccountSettingsService
{
    public async Task<AccountSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var policy = await accountPolicy.GetAsync(ct);
        return new AccountSettingsDto(policy.LocalLoginEnabled, policy.LocalRegistrationEnabled);
    }

    public async Task<AccountSettingsUpdate> UpdateAsync(AccountSettings settings, CancellationToken ct = default)
    {
        var current = await accountPolicy.GetAsync(ct);

        if (current.LocalLoginEnabled && !settings.LocalLoginEnabled && !await ProviderCanLetAnAdminInAsync(ct))
        {
            logger.LogWarning("Refused to disable local sign-in: no administrator has signed in through a provider.");
            return new AccountSettingsUpdate(
                AccountSettingsStatus.WouldLockEveryoneOut,
                new AccountSettingsDto(current.LocalLoginEnabled, current.LocalRegistrationEnabled),
                "Sign in through the identity provider at least once with an administrator account first, " +
                "otherwise disabling local sign-in would leave no way into this instance.");
        }

        // An account that cannot sign in is not worth creating: leaving registration on
        // with local sign-in off would let anyone fill the instance with dead accounts,
        // each one holding an email a provider identity would later collide with.
        var registrationEnabled = settings.LocalLoginEnabled && settings.LocalRegistrationEnabled;

        // A deliberate change is never a bootstrap: registration an administrator turns
        // on stays on until they turn it off (see AccountPolicy).
        await accountPolicy.SetAsync(
            new AccountPolicy(
                settings.LocalLoginEnabled,
                registrationEnabled,
                RegistrationOpenedForBootstrap: false),
            ct);

        logger.LogWarning(
            "Account policy changed: local sign-in {LoginState}, local registration {RegistrationState}.",
            settings.LocalLoginEnabled ? "enabled" : "disabled",
            registrationEnabled ? "enabled" : "disabled");

        return new AccountSettingsUpdate(
            AccountSettingsStatus.Applied,
            new AccountSettingsDto(settings.LocalLoginEnabled, registrationEnabled));
    }

    /// <summary>
    /// Whether an administrator has proven the provider works by signing in with it.
    /// Asked of the recorded identities rather than of the provider's availability, so
    /// a provider that is merely unreachable right now does not undo the proof.
    /// </summary>
    private async Task<bool> ProviderCanLetAnAdminInAsync(CancellationToken ct) =>
        provider.ConfiguredIssuer is { } issuer && await users.HasAdminWithExternalLoginAsync(issuer, ct);
}
