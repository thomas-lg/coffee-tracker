using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoffeeTracker.Tests;

// The account policy against real (in-memory) SQLite: what an instance's policy is
// before anyone sets one, and that the seed is written exactly once. The seeding rule
// is the upgrade path — get it wrong and every existing deployment either locks its
// users out or reopens registration behind their back — so it is pinned here rather
// than only exercised end-to-end.
public sealed class AccountPolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AccountPolicyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(_options);

    private void AddUser(string id = "user-1")
    {
        using var db = NewContext();
        db.Users.Add(new AppUser { Id = id, UserName = $"{id}@example.com", Email = $"{id}@example.com" });
        db.SaveChanges();
    }

    [Fact]
    public async Task Fresh_instance_is_seeded_open_for_bootstrap()
    {
        await using var db = NewContext();

        await AccountPolicySeeder.SeedAsync(db, legacyRegistrationEnabled: false);

        var policy = await new EfAccountPolicy(NewContext()).GetAsync();
        Assert.True(policy.LocalLoginEnabled);
        Assert.True(policy.LocalRegistrationEnabled);
        Assert.True(policy.RegistrationOpenedForBootstrap);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Upgraded_instance_keeps_its_registration_posture(bool legacyRegistrationEnabled)
    {
        AddUser();
        await using var db = NewContext();

        await AccountPolicySeeder.SeedAsync(db, legacyRegistrationEnabled);

        var policy = await new EfAccountPolicy(NewContext()).GetAsync();
        // Sign-in was always on before the policy existed; it must stay on whatever the
        // legacy flag said, or upgrading locks every user out of an existing instance.
        Assert.True(policy.LocalLoginEnabled);
        Assert.Equal(legacyRegistrationEnabled, policy.LocalRegistrationEnabled);
        // Not a bootstrap: an upgraded instance must never close registration by itself.
        Assert.False(policy.RegistrationOpenedForBootstrap);
    }

    [Fact]
    public async Task Seeding_is_a_one_shot_and_never_overwrites_a_set_policy()
    {
        await using (var db = NewContext())
        {
            await AccountPolicySeeder.SeedAsync(db, legacyRegistrationEnabled: true);
        }

        await new EfAccountPolicy(NewContext()).SetAsync(
            new AccountPolicy(LocalLoginEnabled: false, LocalRegistrationEnabled: false));

        // A restart with the legacy variable flipped must change nothing.
        await using (var db = NewContext())
        {
            await AccountPolicySeeder.SeedAsync(db, legacyRegistrationEnabled: true);
        }

        var policy = await new EfAccountPolicy(NewContext()).GetAsync();
        Assert.False(policy.LocalLoginEnabled);
        Assert.False(policy.LocalRegistrationEnabled);
    }

    [Fact]
    public async Task An_unseeded_instance_never_reports_sign_in_as_disabled()
    {
        AddUser();

        // No row at all — a context that was never seeded. Reporting sign-in as off here
        // would lock an instance out on the strength of a missing row.
        var policy = await new EfAccountPolicy(NewContext()).GetAsync();

        Assert.True(policy.LocalLoginEnabled);
        Assert.False(policy.LocalRegistrationEnabled);
    }

    [Fact]
    public async Task Set_then_get_round_trips_both_settings()
    {
        await new EfAccountPolicy(NewContext()).SetAsync(
            new AccountPolicy(LocalLoginEnabled: false, LocalRegistrationEnabled: true));

        var policy = await new EfAccountPolicy(NewContext()).GetAsync();

        Assert.False(policy.LocalLoginEnabled);
        Assert.True(policy.LocalRegistrationEnabled);
        // An administrator turning registration on is not a bootstrap, so it must not
        // close itself after the next account.
        Assert.False(policy.RegistrationOpenedForBootstrap);
    }
}
