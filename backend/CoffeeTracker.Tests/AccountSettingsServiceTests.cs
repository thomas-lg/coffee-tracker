using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoffeeTracker.Tests;

// The lock-out guard. Everything else in the settings use case is a setter; this rule
// is the reason it exists, and getting it wrong locks an operator out of their own
// instance with no route back except SQL.
public sealed class AccountSettingsServiceTests
{
    private const string Issuer = "https://id.example.com";

    private sealed class FakePolicy(AccountPolicy initial) : IAccountPolicy
    {
        public AccountPolicy Current { get; private set; } = initial;

        public Task<AccountPolicy> GetAsync(CancellationToken ct = default) => Task.FromResult(Current);

        public Task SetAsync(AccountPolicy policy, CancellationToken ct = default)
        {
            Current = policy;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider(string? issuer) : IExternalIdentityProvider
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(issuer is not null);

        public string? ConfiguredIssuer => issuer;
    }

    private sealed class FakeUsers(bool hasAdminWithExternalLogin) : IUserDirectory
    {
        public Task<bool> HasAdminWithExternalLoginAsync(string issuer, CancellationToken ct = default) =>
            Task.FromResult(hasAdminWithExternalLogin);

        public Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthUser?> FindByIdAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateUserResult> CreateAsync(NewUser user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsLockedOutAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PasswordCheck> VerifyPasswordAsync(string userId, string password, CancellationToken ct = default) => throw new NotSupportedException();
        public void SpendDecoyVerification(string password) => throw new NotSupportedException();
    }

    private static (AccountSettingsService Service, FakePolicy Policy) Build(
        bool localLoginEnabled = true,
        string? issuer = Issuer,
        bool hasAdminWithExternalLogin = true)
    {
        var policy = new FakePolicy(new AccountPolicy(localLoginEnabled, LocalRegistrationEnabled: true));
        var service = new AccountSettingsService(
            policy,
            new FakeUsers(hasAdminWithExternalLogin),
            new FakeProvider(issuer),
            NullLogger<AccountSettingsService>.Instance);
        return (service, policy);
    }

    [Fact]
    public async Task Disabling_sign_in_is_refused_when_no_provider_is_configured()
    {
        var (service, policy) = Build(issuer: null);

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        Assert.Equal(AccountSettingsStatus.WouldLockEveryoneOut, result.Status);
        Assert.True(policy.Current.LocalLoginEnabled);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task Disabling_sign_in_is_refused_when_no_admin_has_used_the_provider()
    {
        var (service, policy) = Build(hasAdminWithExternalLogin: false);

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        // A configured provider is not proof it works, or that anyone with admin rights
        // can actually get through it.
        Assert.Equal(AccountSettingsStatus.WouldLockEveryoneOut, result.Status);
        Assert.True(policy.Current.LocalLoginEnabled);
    }

    [Fact]
    public async Task Disabling_sign_in_is_applied_once_an_admin_has_used_the_provider()
    {
        var (service, policy) = Build();

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: false, LocalRegistrationEnabled: false));

        Assert.Equal(AccountSettingsStatus.Applied, result.Status);
        Assert.False(policy.Current.LocalLoginEnabled);
    }

    [Fact]
    public async Task Re_enabling_sign_in_is_never_refused()
    {
        var (service, policy) = Build(localLoginEnabled: false, issuer: null, hasAdminWithExternalLogin: false);

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: true, LocalRegistrationEnabled: false));

        // The guard exists to keep a door open; it must never stand in the way of
        // opening one.
        Assert.Equal(AccountSettingsStatus.Applied, result.Status);
        Assert.True(policy.Current.LocalLoginEnabled);
    }

    [Fact]
    public async Task Leaving_sign_in_disabled_is_not_treated_as_a_new_refusal()
    {
        var (service, policy) = Build(localLoginEnabled: false, issuer: null, hasAdminWithExternalLogin: false);

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        // The guard fires on the transition, not on the state: an admin already signed
        // in through some other means must still be able to change registration.
        Assert.Equal(AccountSettingsStatus.Applied, result.Status);
        Assert.True(policy.Current.LocalRegistrationEnabled);
    }

    [Fact]
    public async Task Disabling_registration_is_never_guarded()
    {
        var (service, policy) = Build(issuer: null, hasAdminWithExternalLogin: false);

        var result = await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: true, LocalRegistrationEnabled: false));

        // Closing registration cannot lock anyone out — only sign-in can.
        Assert.Equal(AccountSettingsStatus.Applied, result.Status);
        Assert.False(policy.Current.LocalRegistrationEnabled);
    }

    [Fact]
    public async Task An_applied_change_clears_the_bootstrap_mark()
    {
        var policy = new FakePolicy(new AccountPolicy(
            LocalLoginEnabled: true,
            LocalRegistrationEnabled: true,
            RegistrationOpenedForBootstrap: true));
        var service = new AccountSettingsService(
            policy,
            new FakeUsers(hasAdminWithExternalLogin: true),
            new FakeProvider(Issuer),
            NullLogger<AccountSettingsService>.Instance);

        await service.UpdateAsync(new AccountSettingsDto(LocalLoginEnabled: true, LocalRegistrationEnabled: true));

        // Registration an admin has now taken ownership of must not close itself after
        // the next account.
        Assert.False(policy.Current.RegistrationOpenedForBootstrap);
    }
}
