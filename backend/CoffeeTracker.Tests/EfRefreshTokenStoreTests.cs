using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoffeeTracker.Tests;

// The refresh-token store against real (in-memory) SQLite: verifies the opportunistic
// purge keeps the table bounded without dropping tokens that are still valid (which
// would break rotated-token reuse detection during their validity window).
public sealed class EfRefreshTokenStoreTests : IDisposable
{
    private const string UserId = "user-1";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly MutableClock _clock = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public EfRefreshTokenStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
        // RefreshToken has an FK to AspNetUsers, so seed the owning user.
        db.Users.Add(new AppUser { Id = UserId, UserName = "u", Email = "u@example.com" });
        db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(_options);

    private EfRefreshTokenStore NewStore(AppDbContext db) =>
        new(db, _clock, Options.Create(new JwtOptions { RefreshTokenDays = 14 }), NullLogger<EfRefreshTokenStore>.Instance);

    [Fact]
    public async Task IssueAsync_purges_tokens_past_their_expiry()
    {
        await using (var db = NewContext())
        {
            await NewStore(db).IssueAsync(UserId); // expires at T0 + 14d
        }

        _clock.Now = _clock.Now.AddDays(15); // now past the first token's expiry

        await using (var db = NewContext())
        {
            await NewStore(db).IssueAsync(UserId); // issuing purges the expired one
        }

        await using var check = NewContext();
        Assert.Equal(1, await check.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task IssueAsync_keeps_tokens_that_are_still_valid()
    {
        await using (var db = NewContext())
        {
            await NewStore(db).IssueAsync(UserId);
        }

        _clock.Now = _clock.Now.AddDays(1); // both still within their 14-day life

        await using (var db = NewContext())
        {
            await NewStore(db).IssueAsync(UserId);
        }

        await using var check = NewContext();
        Assert.Equal(2, await check.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task ValidateAndRotateAsync_rejects_a_token_that_is_already_expired()
    {
        string raw;
        await using (var db = NewContext())
        {
            raw = (await NewStore(db).IssueAsync(UserId)).Token;
        }

        _clock.Now = _clock.Now.AddDays(15); // past the 14-day life

        await using (var db = NewContext())
        {
            var result = await NewStore(db).ValidateAndRotateAsync(raw);
            Assert.False(result.Succeeded);
        }

        // Expiry is not reuse: the family stays intact, so other live sessions survive.
        await using var check = NewContext();
        Assert.Null((await check.RefreshTokens.SingleAsync()).RevokedAtUtc);
    }

    // Concurrent rotation of ONE token must mint at most one successor. Before the
    // guarded UPDATE in EfRefreshTokenStore, both callers could read RevokedAtUtc as null,
    // both pass the reuse check and both succeed — handing out two live session families
    // from a single token and silently disarming reuse detection.
    //
    // This needs real parallel connections, so it uses its own shared-cache in-memory
    // database rather than the single-connection fixture above (which would serialise the
    // calls and hide the race entirely). The assertion is an invariant the fixed code
    // always satisfies, so it cannot fail on correct code. Verified the way this repo
    // verifies guards: the pre-fix read-then-write was reinstated and this test went red.
    [Fact]
    public async Task ValidateAndRotateAsync_lets_only_one_of_two_concurrent_callers_win()
    {
        var dbName = $"rotate-{Guid.NewGuid():N}";
        var connectionString = $"DataSource=file:{dbName}?mode=memory&cache=shared;Default Timeout=30";

        // Holding one connection open keeps the shared in-memory database alive.
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using (var seed = new AppDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Users.Add(new AppUser { Id = UserId, UserName = "u", Email = "u@example.com" });
            await seed.SaveChangesAsync();
        }

        string raw;
        await using (var db = new AppDbContext(options))
        {
            raw = (await NewStore(db).IssueAsync(UserId)).Token;
        }

        async Task<bool> Rotate()
        {
            await using var db = new AppDbContext(options);
            return (await NewStore(db).ValidateAndRotateAsync(raw)).Succeeded;
        }

        var results = await Task.WhenAll(Task.Run(Rotate), Task.Run(Rotate));

        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
