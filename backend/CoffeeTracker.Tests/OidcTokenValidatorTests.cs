using System.Security.Claims;
using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// Sign-in through a provider is only as strong as this class. Its own doc comment says
// every check is load-bearing: without audience validation a token minted for any other
// client of the same provider signs you in, without issuer validation any provider does,
// without the signature anything does. So each test here bends exactly one of those and
// expects a refusal.
//
// Until now it was covered only by the Playwright suite, which meant a backend-only run
// could not catch a regression in it.
public sealed class OidcTokenValidatorTests : IAsyncLifetime
{
    private const string ClientId = "coffee-tracker-test";

    private FakeOidcProvider _provider = null!;

    public async Task InitializeAsync() => _provider = await FakeOidcProvider.StartAsync();

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private OidcTokenValidator NewValidator(string? adminClaim = null, string? adminClaimValue = null)
    {
        var options = Options.Create(new OidcOptions
        {
            Authority = _provider.Authority,
            ClientId = ClientId,
            AdminClaim = adminClaim,
            AdminClaimValue = adminClaimValue,
        });

        return new OidcTokenValidator(
            new OidcIdentityProvider(options, NullLogger<OidcIdentityProvider>.Instance),
            options,
            NullLogger<OidcTokenValidator>.Instance);
    }

    [Fact]
    public async Task A_properly_signed_token_yields_the_identity_it_carries()
    {
        var token = _provider.MintIdToken(ClientId, subject: "abc-123", claims:
        [
            new Claim("email", "drinker@example.com"),
            new Claim("email_verified", "true"),
            new Claim("name", "A Drinker"),
            new Claim("jti", "token-1"),
        ]);

        var identity = await NewValidator().ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.Equal("abc-123", identity.Subject);
        Assert.Equal("drinker@example.com", identity.Email);
        Assert.True(identity.EmailVerified);
        Assert.Equal("A Drinker", identity.DisplayName);
        Assert.Equal("token-1", identity.TokenId);
        // The configured authority, not the discovery issuer: external logins are keyed
        // by it, and the two are allowed to differ.
        Assert.Equal(_provider.Authority, identity.Issuer);
    }

    [Fact]
    public async Task A_token_minted_for_another_client_of_the_same_provider_is_refused()
    {
        var token = _provider.MintIdToken(audience: "some-other-app");

        Assert.Null(await NewValidator().ValidateAsync(token));
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused()
    {
        var token = _provider.MintIdToken(ClientId, issuer: "https://issuer.invalid");

        Assert.Null(await NewValidator().ValidateAsync(token));
    }

    [Fact]
    public async Task A_token_signed_with_a_key_the_provider_never_published_is_refused()
    {
        // Same kid, different key: a validator that matched on kid alone without
        // checking the signature would accept this.
        var token = _provider.MintIdToken(ClientId, signWith: FakeOidcProvider.UnpublishedKey());

        Assert.Null(await NewValidator().ValidateAsync(token));
    }

    [Fact]
    public async Task An_expired_token_is_refused_once_it_is_past_the_clock_skew()
    {
        var token = _provider.MintIdToken(ClientId, expires: DateTime.UtcNow.AddMinutes(-5));

        Assert.Null(await NewValidator().ValidateAsync(token));
    }

    [Fact]
    public async Task A_token_with_no_subject_is_refused()
    {
        // Valid in every other way. With no subject there is nothing to key an account
        // to, so accepting it would attach the login to nobody.
        var token = _provider.MintIdToken(ClientId, subject: "");

        Assert.Null(await NewValidator().ValidateAsync(token));
    }

    [Fact]
    public async Task Gibberish_is_refused_rather_than_thrown()
    {
        Assert.Null(await NewValidator().ValidateAsync("not-a-jwt"));
    }

    [Fact]
    public async Task A_token_without_a_jti_is_identified_by_a_hash_of_itself()
    {
        var token = _provider.MintIdToken(ClientId);

        var identity = await NewValidator().ValidateAsync(token);

        Assert.NotNull(identity);
        // A SHA-256 of the token, so the spent-token set never holds usable credentials.
        Assert.Equal(64, identity.TokenId.Length);
        Assert.DoesNotContain(token, identity.TokenId);
    }

    [Fact]
    public async Task Email_verified_is_false_unless_the_provider_says_true()
    {
        var token = _provider.MintIdToken(ClientId, claims: [new Claim("email_verified", "false")]);

        var identity = await NewValidator().ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.False(identity.EmailVerified);
    }

    [Fact]
    public async Task The_display_name_falls_back_to_preferred_username()
    {
        var token = _provider.MintIdToken(ClientId, claims: [new Claim("preferred_username", "drinker")]);

        var identity = await NewValidator().ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.Equal("drinker", identity.DisplayName);
    }

    [Fact]
    public async Task With_no_admin_mapping_configured_the_assertion_is_null_not_false()
    {
        // Null and false mean different things downstream: null hands the decision back
        // to the bootstrap rule, false actively says this user is not an administrator.
        var token = _provider.MintIdToken(ClientId, claims: [new Claim("groups", "admins")]);

        var identity = await NewValidator().ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.Null(identity.AdminAssertion);
    }

    [Fact]
    public async Task A_configured_admin_mapping_matches_any_occurrence_of_a_repeated_claim()
    {
        // groups and roles legitimately repeat, so the match cannot look at the first
        // value alone.
        var token = _provider.MintIdToken(ClientId, claims:
        [
            new Claim("groups", "staff"),
            new Claim("groups", "everyone"),
            new Claim("groups", "coffee-admins"),
        ]);

        var identity = await NewValidator("groups", "coffee-admins").ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.True(identity.AdminAssertion);
    }

    [Fact]
    public async Task A_configured_admin_mapping_that_matches_nothing_asserts_false()
    {
        var token = _provider.MintIdToken(ClientId, claims: [new Claim("groups", "staff")]);

        var identity = await NewValidator("groups", "coffee-admins").ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.False(identity.AdminAssertion);
    }

    [Fact]
    public async Task The_admin_claim_value_is_matched_exactly()
    {
        // Ordinal, not case-insensitive: a group named COFFEE-ADMINS is a different
        // group, and quietly treating it as a match would hand out administration.
        var token = _provider.MintIdToken(ClientId, claims: [new Claim("groups", "COFFEE-ADMINS")]);

        var identity = await NewValidator("groups", "coffee-admins").ValidateAsync(token);

        Assert.NotNull(identity);
        Assert.False(identity.AdminAssertion);
    }
}
