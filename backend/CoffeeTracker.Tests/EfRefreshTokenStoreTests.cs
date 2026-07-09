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

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
