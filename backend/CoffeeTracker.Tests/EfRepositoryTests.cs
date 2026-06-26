using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Domain;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoffeeTracker.Tests;

// Repository tests against a real (in-memory) SQLite database — the InMemory
// provider can't validate correlated-subquery aggregates, unique indexes, or
// cascade deletes, all of which this milestone relies on. Each test gets a fresh
// schema (EnsureCreated also applies the HasData-seeded flavor tags).
public class EfRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public EfRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewContext() => new(_options);

    private async Task<int> SeedCoffeeAsync()
    {
        await using var db = NewContext();
        var coffee = new Coffee
        {
            Name = "Geisha",
            Roaster = "R",
            Origin = "Panama",
            RoastLevel = "Light",
            Price = 30m,
            DateBought = new DateOnly(2026, 6, 20),
            CreatedAt = DateTimeOffset.UnixEpoch,
        };
        db.Coffees.Add(coffee);
        await db.SaveChangesAsync();
        return coffee.Id;
    }

    private async Task AddReviewAsync(int coffeeId, string userId, int rating, params int[] tagIds)
    {
        await using var db = NewContext();
        var repo = new EfReviewRepository(db);
        var tags = await new EfFlavorTagRepository(db).GetByIdsAsync(tagIds);
        await repo.AddAsync(new Review
        {
            CoffeeId = coffeeId,
            UserId = userId,
            Rating = rating,
            CreatedAt = DateTimeOffset.UnixEpoch,
            Tags = tags.ToList(),
        });
    }

    [Fact]
    public async Task GetWithStatsByIdAsync_ComputesAverageAndCount()
    {
        var coffeeId = await SeedCoffeeAsync();
        await AddReviewAsync(coffeeId, "u1", 4);
        await AddReviewAsync(coffeeId, "u2", 2);

        await using var db = NewContext();
        var result = await new EfCoffeeRepository(db).GetWithStatsByIdAsync(coffeeId);

        Assert.NotNull(result);
        Assert.Equal(3.0, result!.AverageRating);
        Assert.Equal(2, result.ReviewCount);
    }

    [Fact]
    public async Task GetWithStatsByIdAsync_ReturnsNullAverage_WhenNoReviews()
    {
        var coffeeId = await SeedCoffeeAsync();

        await using var db = NewContext();
        var result = await new EfCoffeeRepository(db).GetWithStatsByIdAsync(coffeeId);

        Assert.NotNull(result);
        Assert.Null(result!.AverageRating);
        Assert.Equal(0, result.ReviewCount);
    }

    [Fact]
    public async Task GetAllAsync_CarriesStats_NewestFirst()
    {
        var c1 = await SeedCoffeeAsync();
        var c2 = await SeedCoffeeAsync();
        await AddReviewAsync(c1, "u1", 5);

        await using var db = NewContext();
        var all = await new EfCoffeeRepository(db).GetAllAsync();

        Assert.Equal(c2, all[0].Coffee.Id); // newest (highest id) first
        Assert.Equal(0, all[0].ReviewCount);
        Assert.Equal(5.0, all.Single(x => x.Coffee.Id == c1).AverageRating);
    }

    [Fact]
    public async Task AddAsync_PersistsTags_FromSeededSet()
    {
        var coffeeId = await SeedCoffeeAsync();
        await AddReviewAsync(coffeeId, "u1", 4, 1, 3); // Fruity, Citrus

        await using var db = NewContext();
        var reviews = await new EfReviewRepository(db).GetByCoffeeAsync(coffeeId);

        var tags = Assert.Single(reviews).Tags.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Citrus", "Fruity"], tags);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTags()
    {
        var coffeeId = await SeedCoffeeAsync();
        await AddReviewAsync(coffeeId, "u1", 4, 1, 3);

        // Re-load tracked, replace tags with a different set, save.
        await using (var db = NewContext())
        {
            var repo = new EfReviewRepository(db);
            var review = await repo.GetByIdAsync((await repo.GetByCoffeeAsync(coffeeId))[0].Id);
            var newTags = await new EfFlavorTagRepository(db).GetByIdsAsync([6]); // Nutty
            review!.Tags.Clear();
            foreach (var t in newTags)
            {
                review.Tags.Add(t);
            }
            await repo.UpdateAsync(review);
        }

        await using var verify = NewContext();
        var reloaded = (await new EfReviewRepository(verify).GetByCoffeeAsync(coffeeId))[0];
        Assert.Equal(["Nutty"], reloaded.Tags.Select(t => t.Name));
    }

    [Fact]
    public async Task AddAsync_ThrowsDuplicateReviewException_OnSecondReviewBySameUser()
    {
        var coffeeId = await SeedCoffeeAsync();
        await AddReviewAsync(coffeeId, "u1", 4);

        await using var db = NewContext();
        var repo = new EfReviewRepository(db);

        await Assert.ThrowsAsync<DuplicateReviewException>(() => repo.AddAsync(new Review
        {
            CoffeeId = coffeeId,
            UserId = "u1",
            Rating = 1,
            CreatedAt = DateTimeOffset.UnixEpoch,
        }));
    }

    [Fact]
    public async Task DeletingCoffee_CascadeDeletesItsReviews()
    {
        var coffeeId = await SeedCoffeeAsync();
        await AddReviewAsync(coffeeId, "u1", 4, 1);

        await using (var db = NewContext())
        {
            await new EfCoffeeRepository(db).DeleteAsync(coffeeId);
        }

        await using var verify = NewContext();
        Assert.Empty(await new EfReviewRepository(verify).GetByCoffeeAsync(coffeeId));
        Assert.False(await verify.Reviews.AnyAsync());
    }
}
