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

        var user = await ApplyAdminPolicyAsync(resolved.User, identity, ct);

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
    private async Task<(ExternalSignInStatus Status, AuthUser? User)> ResolveAccountAsync(
        ExternalIdentity identity,
        CancellationToken ct)
    {
        var existing = await users.FindByExternalLoginAsync(identity.Issuer, identity.Subject, ct);
        if (existing is not null)
        {
            return (ExternalSignInStatus.Success, existing);
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
                    return (ExternalSignInStatus.UnverifiedEmailConflict, null);
                }

                await users.LinkExternalLoginAsync(byEmail.Id, identity.Issuer, identity.Subject, ct);
                logger.LogWarning(
                    "Linked provider identity to existing account {UserId} on a verified email.",
                    byEmail.Id);
                return (ExternalSignInStatus.Success, byEmail);
            }
        }

        var created = await users.CreateFromExternalAsync(
            identity.Issuer,
            identity.Subject,
            // A provider that asserts no email still needs a unique, stable local
            // identifier; the subject is both.
            identity.Email ?? $"{identity.Subject}@{new Uri(identity.Issuer).Host}",
            identity.DisplayName ?? identity.Subject,
            ct);

        if (created.User is null)
        {
            logger.LogWarning("Provider sign-in failed to create an account: {Error}.", created.Error);
            return (ExternalSignInStatus.InvalidToken, null);
        }

        return (ExternalSignInStatus.Success, created.User);
    }

    /// <summary>
    /// Applies the configured claim mapping on every sign-in, granting *and* revoking.
    /// A flag that could only ever be set would make the provider unable to take admin
    /// rights back, which is the main reason to centralise identity in the first place.
    /// With no mapping configured, the account keeps whatever the local bootstrap gave it.
    /// </summary>
    private async Task<AuthUser> ApplyAdminPolicyAsync(AuthUser user, ExternalIdentity identity, CancellationToken ct)
    {
        if (identity.AdminAssertion is not { } asserted || asserted == user.IsAdmin)
        {
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
