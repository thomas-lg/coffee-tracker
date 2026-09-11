using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoffeeTracker.Tests;

// Account resolution and administrator assignment for a provider sign-in. These are
// the rules the adapter cannot hold: which human an assertion belongs to, and what
// their rights are. Both have a wrong answer that silently loses or hands over data.
public sealed class ExternalSignInServiceTests
{
    private const string Issuer = "https://id.example.com";
    private const string Subject = "provider-subject-1";
    private const string TokenId = "token-1";

    private sealed class FakeValidator(ExternalIdentity? identity) : IExternalTokenValidator
    {
        public Task<ExternalIdentity?> ValidateAsync(string idToken, CancellationToken ct = default) =>
            Task.FromResult(identity);
    }

    private sealed class FakeUsedTokens : IUsedTokenRegistry
    {
        private readonly HashSet<string> _spent = [];

        // Refusing the second call is what the replay test turns on, so the fake has to
        // honour single use rather than always say yes.
        public Task<bool> TryConsumeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            Task.FromResult(_spent.Add(tokenId));
    }

    private sealed class FakeUsers : StubUserDirectory
    {
        public AuthUser? ByExternalLogin { get; init; }
        public AuthUser? ByEmail { get; init; }
        public AuthUser? Created { get; init; }

        public List<(string UserId, string Issuer, string Subject)> Links { get; } = [];
        public List<(string UserId, bool IsAdmin)> AdminChanges { get; } = [];
        public bool CreateCalled { get; private set; }

        public override Task<AuthUser?> FindByExternalLoginAsync(string issuer, string subject, CancellationToken ct = default) =>
            Task.FromResult(ByExternalLogin);

        public override Task<AuthUser?> FindByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(ByEmail);

        public override Task LinkExternalLoginAsync(string userId, string issuer, string subject, CancellationToken ct = default)
        {
            Links.Add((userId, issuer, subject));
            return Task.CompletedTask;
        }

        public override Task<CreateUserResult> CreateFromExternalAsync(
            string issuer, string subject, string email, string displayName, CancellationToken ct = default)
        {
            CreateCalled = true;
            return Task.FromResult(CreateUserResult.Ok(Created!));
        }

        public override Task SetAdminAsync(string userId, bool isAdmin, CancellationToken ct = default)
        {
            AdminChanges.Add((userId, isAdmin));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokens : ITokenIssuer
    {
        public AccessToken CreateAccessToken(AuthUser user) =>
            new($"access-for-{user.Id}", DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private sealed class FakeRefreshTokens : IRefreshTokenStore
    {
        public Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(new IssuedRefreshToken($"refresh-for-{userId}", DateTimeOffset.UtcNow.AddDays(14)));

        public Task<RefreshRotation> ValidateAndRotateAsync(string presentedToken, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task RevokeAsync(string presentedToken, CancellationToken ct = default) => throw new NotSupportedException();

        public Task RevokeAllAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static ExternalIdentity Identity(
        string? email = "person@example.com",
        bool emailVerified = true,
        bool? adminAssertion = null,
        string tokenId = TokenId) =>
        new(Issuer, Subject, tokenId, DateTimeOffset.UtcNow.AddMinutes(5), email, emailVerified, "A Person", adminAssertion);

    private static (ExternalSignInService Service, FakeUsers Users) Build(
        ExternalIdentity? identity,
        FakeUsers users)
    {
        var service = new ExternalSignInService(
            new StubIdentityProvider(Issuer),
            new FakeValidator(identity),
            new FakeUsedTokens(),
            users,
            new FakeTokens(),
            new FakeRefreshTokens(),
            NullLogger<ExternalSignInService>.Instance);
        return (service, users);
    }

    [Fact]
    public async Task A_returning_identity_resolves_by_subject_not_email()
    {
        var known = new AuthUser("user-1", "old@example.com", "A Person", IsAdmin: false);
        var (service, users) = Build(
            // The provider now asserts a different address than the account holds.
            Identity(email: "brand-new@example.com"),
            new FakeUsers { ByExternalLogin = known });

        var result = await service.SignInAsync("token");

        Assert.Equal(ExternalSignInStatus.Success, result.Status);
        Assert.Equal("user-1", result.Response!.UserId);
        // Matching on the subject means a changed email never strands someone's data.
        Assert.Empty(users.Links);
        Assert.False(users.CreateCalled);
    }

    [Fact]
    public async Task A_first_sign_in_links_to_an_existing_account_on_a_verified_email()
    {
        var existing = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: true);
        var (service, users) = Build(Identity(emailVerified: true), new FakeUsers { ByEmail = existing });

        var result = await service.SignInAsync("token");

        Assert.Equal(ExternalSignInStatus.Success, result.Status);
        Assert.Equal("user-1", result.Response!.UserId);
        Assert.Equal(("user-1", Issuer, Subject), users.Links.Single());
        Assert.False(users.CreateCalled);
    }

    [Fact]
    public async Task An_unverified_matching_email_is_refused_rather_than_linked_or_duplicated()
    {
        var existing = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: true);
        var (service, users) = Build(Identity(emailVerified: false), new FakeUsers { ByEmail = existing });

        var result = await service.SignInAsync("token");

        // Linking would hand this account to anyone who can make a provider claim the
        // address; creating a second account would orphan everything the first owns.
        Assert.Equal(ExternalSignInStatus.UnverifiedEmailConflict, result.Status);
        Assert.Empty(users.Links);
        Assert.False(users.CreateCalled);
    }

    [Fact]
    public async Task An_unknown_identity_creates_an_account()
    {
        var created = new AuthUser("user-new", "person@example.com", "A Person", IsAdmin: true);
        var (service, users) = Build(Identity(), new FakeUsers { Created = created });

        var result = await service.SignInAsync("token");

        Assert.Equal(ExternalSignInStatus.Success, result.Status);
        Assert.True(users.CreateCalled);
        // The bootstrap that promotes the first account applies to provider sign-ins too.
        Assert.True(result.Response!.IsAdmin);
    }

    [Fact]
    public async Task The_admin_claim_grants_and_revokes_on_every_sign_in()
    {
        var plain = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: false);
        var (grant, grantUsers) = Build(Identity(adminAssertion: true), new FakeUsers { ByExternalLogin = plain });

        var granted = await grant.SignInAsync("token");
        Assert.True(granted.Response!.IsAdmin);
        Assert.Equal(("user-1", true), grantUsers.AdminChanges.Single());

        var admin = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: true);
        var (revoke, revokeUsers) = Build(Identity(adminAssertion: false), new FakeUsers { ByExternalLogin = admin });

        var revoked = await revoke.SignInAsync("token");
        // Rights that can only ever be granted cannot be taken back from the provider,
        // which would defeat the point of centralising identity there.
        Assert.False(revoked.Response!.IsAdmin);
        Assert.Equal(("user-1", false), revokeUsers.AdminChanges.Single());
    }

    [Fact]
    public async Task Without_a_claim_mapping_the_local_admin_flag_is_left_alone()
    {
        var admin = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: true);
        var (service, users) = Build(Identity(adminAssertion: null), new FakeUsers { ByExternalLogin = admin });

        var result = await service.SignInAsync("token");

        Assert.True(result.Response!.IsAdmin);
        // Silence from an unconfigured mapping must not be read as "not an admin".
        Assert.Empty(users.AdminChanges);
    }

    [Fact]
    public async Task A_token_that_does_not_validate_is_refused()
    {
        var (service, _) = Build(identity: null, new FakeUsers());

        var result = await service.SignInAsync("token");

        Assert.Equal(ExternalSignInStatus.InvalidToken, result.Status);
    }

    [Fact]
    public async Task A_token_buys_at_most_one_session()
    {
        var known = new AuthUser("user-1", "person@example.com", "A Person", IsAdmin: false);
        var (service, _) = Build(Identity(), new FakeUsers { ByExternalLogin = known });

        Assert.Equal(ExternalSignInStatus.Success, (await service.SignInAsync("token")).Status);

        // Same token, replayed. Single use is the whole protection against a token
        // captured in a log or a proxy being turned into a session.
        Assert.Equal(ExternalSignInStatus.InvalidToken, (await service.SignInAsync("token")).Status);
    }
}
