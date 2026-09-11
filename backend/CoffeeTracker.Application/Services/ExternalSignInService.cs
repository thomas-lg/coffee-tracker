using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.Extensions.Logging;

namespace CoffeeTracker.Application.Services;

/// <summary>
/// Sign-in through the external provider. Owns the two rules that cannot live in the
/// adapter: which account an asserted identity resolves to, and who is an
/// administrator. The token's believability is the validator's business.
/// </summary>
public sealed class ExternalSignInService(
    IExternalIdentityProvider provider,
    IExternalTokenValidator validator,
    IUsedTokenRegistry usedTokens,
    IUserDirectory users,
    IAccountPolicy accountPolicy,
    ITokenIssuer tokenIssuer,
    IRefreshTokenStore refreshTokens,
    ILogger<ExternalSignInService> logger) : IExternalSignInService
{
    public async Task<ExternalSignInResult> SignInAsync(string idToken, CancellationToken ct = default)
    {
        if (provider.ConfiguredIssuer is null)
        {
            return ExternalSignInResult.Fail(ExternalSignInStatus.NotConfigured);
        }

        var identity = await validator.ValidateAsync(idToken, ct);
        if (identity is null)
        {
            logger.LogWarning("Provider sign-in refused: the ID token did not validate.");
            return ExternalSignInResult.Fail(ExternalSignInStatus.InvalidToken);
        }

        // One token, one session. A token captured in a log or a proxy stays valid for
        // minutes; spending it here is what stops it being exchanged a second time.
        if (!await usedTokens.TryConsumeAsync(identity.TokenId, identity.ExpiresAt, ct))
        {
            logger.LogWarning("Provider sign-in refused: this ID token has already been exchanged.");
            return ExternalSignInResult.Fail(ExternalSignInStatus.InvalidToken);
        }

        var resolved = await ResolveAccountAsync(identity, ct);
        if (resolved.Status is not ExternalSignInStatus.Success || resolved.User is null)
        {
            return ExternalSignInResult.Fail(resolved.Status);
        }

        var user = await ApplyAdminPolicyAsync(resolved.User, identity, resolved.WasCreated, ct);

        if (resolved.WasCreated && user.IsAdmin)
        {
            await CloseBootstrapRegistrationAsync(ct);
        }

        var access = tokenIssuer.CreateAccessToken(user);
        var refresh = await refreshTokens.IssueAsync(user.Id, ct);
        return ExternalSignInResult.Success(new AuthResponseDto(
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            user.Id,
            user.DisplayName,
            user.IsAdmin));
    }

    /// <summary>
    /// Resolves the identity to an account: by the stable issuer+subject pair first, then
    /// by a verified email, and otherwise by creating one. Emails change and are not
    /// guaranteed unique across providers, which is why they are only ever a fallback.
    /// </summary>
    private async Task<(ExternalSignInStatus Status, AuthUser? User, bool WasCreated)> ResolveAccountAsync(
        ExternalIdentity identity,
        CancellationToken ct)
    {
        var existing = await users.FindByExternalLoginAsync(identity.Issuer, identity.Subject, ct);
        if (existing is not null)
        {
            return (ExternalSignInStatus.Success, existing, false);
        }

        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            var byEmail = await users.FindByEmailAsync(identity.Email, ct);
            if (byEmail is not null)
            {
                if (!identity.EmailVerified)
                {
                    // Linking here would hand this account to whoever can make the provider
                    // assert that address; creating a second account would silently strand
                    // everything the first one owns. Neither is acceptable, so refuse.
                    logger.LogWarning(
                        "Provider sign-in refused for user {UserId}: the matching email is not asserted as verified.",
                        byEmail.Id);
                    return (ExternalSignInStatus.UnverifiedEmailConflict, null, false);
                }

                await users.LinkExternalLoginAsync(byEmail.Id, identity.Issuer, identity.Subject, ct);
                logger.LogWarning(
                    "Linked provider identity to existing account {UserId} on a verified email.",
                    byEmail.Id);
                return (ExternalSignInStatus.Success, byEmail, false);
            }
        }

        var created = await users.CreateFromExternalAsync(
            identity.Issuer,
            identity.Subject,
            // A provider that asserts no email still needs a unique, stable local
            // identifier; the subject is both.
            identity.Email ?? SyntheticEmailFor(identity),
            identity.DisplayName ?? identity.Subject,
            ct);

        if (created.User is null)
        {
            logger.LogWarning("Provider sign-in failed to create an account: {Error}.", created.Error);
            return (ExternalSignInStatus.InvalidToken, null, false);
        }

        return (ExternalSignInStatus.Success, created.User, true);
    }

    /// <summary>
    /// Shuts the door a fresh instance left open for its first account, now that the
    /// account exists. AuthService does the same after a local registration; without it
    /// here, an instance whose first user arrives through the provider keeps accepting
    /// anonymous sign-ups. Registration an administrator opened deliberately carries no
    /// bootstrap mark and is left alone.
    /// </summary>
    private async Task CloseBootstrapRegistrationAsync(CancellationToken ct)
    {
        var policy = await accountPolicy.GetAsync(ct);
        if (policy.RegistrationOpenedForBootstrap)
        {
            await accountPolicy.SetAsync(
                policy with { LocalRegistrationEnabled = false, RegistrationOpenedForBootstrap = false },
                ct);
        }
    }

    /// <summary>
    /// A stable, unique local identifier for a provider that asserts no email. Built
    /// from the issuer's host when it is a URL and the whole issuer otherwise — an
    /// issuer is only required to be a case-sensitive string, and parsing one as a URI
    /// unguarded turns a compliant provider into a 500.
    /// </summary>
    private static string SyntheticEmailFor(ExternalIdentity identity)
    {
        var host = Uri.TryCreate(identity.Issuer, UriKind.Absolute, out var uri)
            ? uri.Host
            : new string(identity.Issuer.Where(char.IsLetterOrDigit).ToArray());
        return $"{identity.Subject}@{host}.invalid";
    }

    /// <summary>
    /// Applies the configured claim mapping on every sign-in, granting *and* revoking.
    /// A flag that could only ever be set would make the provider unable to take admin
    /// rights back, which is the main reason to centralise identity in the first place.
    /// With no mapping configured, the account keeps whatever the local bootstrap gave it.
    /// </summary>
    private async Task<AuthUser> ApplyAdminPolicyAsync(
        AuthUser user,
        ExternalIdentity identity,
        bool wasCreated,
        CancellationToken ct)
    {
        if (identity.AdminAssertion is not { } asserted || asserted == user.IsAdmin)
        {
            return user;
        }

        if (wasCreated && user.IsAdmin && !asserted)
        {
            // This account was just promoted by the first-user bootstrap and the claim
            // mapping would immediately take it back, leaving an instance with no
            // administrator at all and no way to appoint one from inside the app. The
            // bootstrap wins; the provider governs every sign-in after this one.
            logger.LogWarning(
                "Kept administrator on {UserId}: the provider's claim would have left this instance with none.",
                user.Id);
            return user;
        }

        await users.SetAdminAsync(user.Id, asserted, ct);
        logger.LogWarning(
            "Administrator status for {UserId} set to {IsAdmin} by the provider's claim.",
            user.Id,
            asserted);
        return user with { IsAdmin = asserted };
    }
}
