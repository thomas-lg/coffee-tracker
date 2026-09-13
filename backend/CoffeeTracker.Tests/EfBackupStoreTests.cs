using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoffeeTracker.Tests;

// Against real SQLite, because what is being checked is the part a fake cannot show:
// that a restore round-trips through actual rows, that it really replaces rather than
// appends, and that the tag join survives the trip.
public sealed class EfBackupStoreTests : IDisposable
{
    /// <summary>How many tags the model seeds — the closed set the UI renders as chips.</summary>
    private const int SeededTags = 10;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public EfBackupStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var db = NewContext();
        // EnsureCreated applies the model's HasData, so the ten flavour tags are already
        // there — they are seeded reference data, and a backup names them.
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(_options);

    private static Coffee Coffee(string name) => new()
    {
        Name = name,
        Roaster = "La Cabra",
        Origin = "Kenya",
        RoastLevel = RoastLevel.Light,
        Price = 18.5m,
        DateBought = new DateOnly(2026, 8, 28),
        CreatedByUserId = "user-1",
        CreatedAt = new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero),
    };

    private static Review Review(int rating, params string[] tags) => new()
    {
        UserId = "user-1",
        Rating = rating,
        TastingNotes = "Blackcurrant.",
        CreatedAt = new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
        Tags = [.. tags.Select(t => new FlavorTag { Name = t })],
    };

    private async Task<ReplaceResult> Restore(params CoffeeWithReviews[] coffees)
    {
        await using var db = NewContext();
        return await new EfBackupStore(db).ReplaceAllAsync(coffees);
    }

    private async Task<IReadOnlyList<CoffeeWithReviews>> Export()
    {
        await using var db = NewContext();
        return await new EfBackupStore(db).ExportAsync();
    }

    [Fact]
    public async Task A_restored_catalog_exports_back_the_same_way()
    {
        await Restore(new CoffeeWithReviews(Coffee("Kirinyaga AA"), [Review(5, "Berry", "Citrus")]));

        var exported = await Export();

        var entry = Assert.Single(exported);
        Assert.Equal("Kirinyaga AA", entry.Coffee.Name);
        Assert.Equal("user-1", entry.Coffee.CreatedByUserId);
        var review = Assert.Single(entry.Reviews);
        Assert.Equal(5, review.Rating);
        // The join row survived, matched by name onto the seeded tags.
        Assert.Equal(["Berry", "Citrus"], review.Tags.Select(t => t.Name).Order());
    }

    // "Replace", not "append": restoring twice must not leave two shelves.
    [Fact]
    public async Task Restoring_replaces_what_was_there_rather_than_adding_to_it()
    {
        await Restore(new CoffeeWithReviews(Coffee("First"), [Review(5)]));
        await Restore(new CoffeeWithReviews(Coffee("Second"), [Review(4)]));

        var exported = await Export();

        Assert.Equal("Second", Assert.Single(exported).Coffee.Name);
        await using var db = NewContext();
        // The old reviews went with the old coffee rather than lingering unreferenced.
        Assert.Equal(1, await db.Reviews.CountAsync());
    }

    [Fact]
    public async Task Restoring_an_empty_backup_clears_the_catalog()
    {
        await Restore(new CoffeeWithReviews(Coffee("Kirinyaga AA"), [Review(5)]));

        await Restore();

        Assert.Empty(await Export());
        await using var db = NewContext();
        Assert.Equal(0, await db.Reviews.CountAsync());
        // Tags are reference data, not catalog: a restore must not wipe them.
        Assert.Equal(SeededTags, await db.FlavorTags.CountAsync());
    }

    [Fact]
    public async Task Reviews_follow_the_coffee_they_belong_to()
    {
        await Restore(
            new CoffeeWithReviews(Coffee("One"), [Review(5)]),
            new CoffeeWithReviews(Coffee("Two"), [Review(4), Review(3)]));

        var exported = await Export();

        Assert.Equal(2, exported.Count);
        // Ids are assigned on insert, so a review pointing at the wrong coffee is the
        // realistic failure here — not a missing row.
        Assert.Single(exported.First(c => c.Coffee.Name == "One").Reviews);
        Assert.Equal(2, exported.First(c => c.Coffee.Name == "Two").Reviews.Count);
    }

    [Fact]
    public async Task An_unknown_tag_is_skipped_and_reported_rather_than_created()
    {
        var result = await Restore(new CoffeeWithReviews(Coffee("Kirinyaga AA"), [Review(5, "Berry", "Smoky")]));

        Assert.Contains("Smoky", Assert.Single(result.Warnings));

        var review = Assert.Single(Assert.Single(await Export()).Reviews);
        Assert.Equal(["Berry"], review.Tags.Select(t => t.Name));
        await using var db = NewContext();
        // Inventing the tag would put a chip on screen that no filter can ever match.
        Assert.Equal(SeededTags, await db.FlavorTags.CountAsync());
    }

    [Fact]
    public async Task Tag_names_are_matched_regardless_of_casing()
    {
        // The seed's casing is a display choice, not an identity.
        var result = await Restore(new CoffeeWithReviews(Coffee("Kirinyaga AA"), [Review(5, "BERRY")]));

        Assert.Empty(result.Warnings);
        Assert.Equal("Berry", Assert.Single(Assert.Single(await Export()).Reviews).Tags.Single().Name);
    }

    [Fact]
    public async Task The_counts_reported_are_what_was_written()
    {
        var result = await Restore(
            new CoffeeWithReviews(Coffee("One"), [Review(5)]),
            new CoffeeWithReviews(Coffee("Two"), [Review(4), Review(3)]));

        Assert.Equal(2, result.Coffees);
        Assert.Equal(3, result.Reviews);
    }
}
