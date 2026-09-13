using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests;

// A restore is the one operation that writes rows nobody typed into a form, so the
// checks the API boundary normally performs have to happen here instead. It is also
// destructive, which makes "refused before anything was written" the property that
// matters most in this file.
public sealed class BackupServiceTests
{
    private readonly FakeBackupStore _store = new();

    private BackupService NewService() =>
        new(_store, new FixedClock(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero)));

    private static BackupCoffeeDto Coffee(
        string name = "Kirinyaga AA",
        RoastLevel roast = RoastLevel.Light,
        decimal price = 18.5m,
        params BackupReviewDto[] reviews) => new(
            name, "La Cabra", "Kenya", roast, price,
            new DateOnly(2026, 8, 28), null, null, null, "user-1",
            new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero), reviews);

    private static BackupReviewDto Review(int rating = 5, string userId = "user-1") => new(
        userId, rating, "Fresh bag", "Blackcurrant.", null, null, null,
        new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero), null, ["Berry"]);

    private static BackupDto Backup(params BackupCoffeeDto[] coffees) =>
        new(BackupDto.CurrentFormatVersion, DateTimeOffset.UtcNow, coffees);

    [Fact]
    public async Task Export_carries_the_catalog_with_its_reviews_and_tag_names()
    {
        _store.Catalog =
        [
            new CoffeeWithReviews(
                new Coffee
                {
                    Id = 7,
                    Name = "Kirinyaga AA",
                    Roaster = "La Cabra",
                    Origin = "Kenya",
                    RoastLevel = RoastLevel.Light,
                    Price = 18.5m,
                    DateBought = new DateOnly(2026, 8, 28),
                    CreatedByUserId = "user-1",
                },
                [new Review { UserId = "user-1", Rating = 5, Tags = [new FlavorTag { Id = 2, Name = "Berry" }] }]),
        ];

        var export = await NewService().ExportAsync();

        Assert.Equal(BackupDto.CurrentFormatVersion, export.FormatVersion);
        var coffee = Assert.Single(export.Coffees);
        Assert.Equal("Kirinyaga AA", coffee.Name);
        Assert.Equal("user-1", coffee.CreatedByUserId);
        // Tags travel by name: ids are per-instance, names are the stable part.
        Assert.Equal(["Berry"], Assert.Single(coffee.Reviews).Tags);
    }

    [Fact]
    public async Task A_file_from_a_newer_instance_is_refused_by_version()
    {
        var outcome = await NewService().ImportAsync(
            new BackupDto(BackupDto.CurrentFormatVersion + 1, DateTimeOffset.UtcNow, []));

        Assert.Equal(ImportStatus.UnsupportedFormat, outcome.Status);
        // Both versions are named, because "upgrade first" is only obvious once you can
        // see which is which.
        Assert.Contains($"version {BackupDto.CurrentFormatVersion + 1}", outcome.Reason);
        Assert.False(_store.Replaced);
    }

    [Fact]
    public async Task Something_that_is_not_a_backup_is_refused()
    {
        var outcome = await NewService().ImportAsync(null);

        Assert.Equal(ImportStatus.UnsupportedFormat, outcome.Status);
        Assert.False(_store.Replaced);
    }

    [Theory]
    [InlineData("", "no name")]
    [InlineData("   ", "no name")]
    public async Task A_coffee_without_a_name_is_refused(string name, string expected)
    {
        var outcome = await NewService().ImportAsync(Backup(Coffee(name: name)));

        Assert.Equal(ImportStatus.Invalid, outcome.Status);
        Assert.Contains(expected, outcome.Reason);
        Assert.False(_store.Replaced);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task A_review_rated_outside_one_to_five_is_refused(int rating)
    {
        var outcome = await NewService().ImportAsync(Backup(Coffee(reviews: Review(rating))));

        Assert.Equal(ImportStatus.Invalid, outcome.Status);
        Assert.Contains("1 to 5", outcome.Reason);
        Assert.False(_store.Replaced);
    }

    [Fact]
    public async Task An_unknown_roast_level_is_refused()
    {
        var outcome = await NewService().ImportAsync(Backup(Coffee(roast: (RoastLevel)99)));

        Assert.Equal(ImportStatus.Invalid, outcome.Status);
        Assert.False(_store.Replaced);
    }

    [Fact]
    public async Task A_negative_price_is_refused()
    {
        var outcome = await NewService().ImportAsync(Backup(Coffee(price: -1m)));

        Assert.Equal(ImportStatus.Invalid, outcome.Status);
        Assert.False(_store.Replaced);
    }

    // The property that matters most: validation runs over the whole file first, so a
    // bad row near the end cannot leave the instance holding neither the old catalog
    // nor a complete new one.
    [Fact]
    public async Task Nothing_is_written_when_a_later_coffee_is_the_invalid_one()
    {
        var outcome = await NewService().ImportAsync(Backup(
            Coffee(name: "Fine"),
            Coffee(name: "Also fine"),
            Coffee(name: "")));

        Assert.Equal(ImportStatus.Invalid, outcome.Status);
        Assert.Contains("Coffee 3", outcome.Reason);
        Assert.False(_store.Replaced);
    }

    [Fact]
    public async Task A_valid_backup_replaces_the_catalog_and_reports_what_it_wrote()
    {
        _store.Result = new ReplaceResult(2, 3, []);

        var outcome = await NewService().ImportAsync(Backup(
            Coffee(name: "One", reviews: Review()),
            Coffee("Two", RoastLevel.Light, 18.5m, Review(), Review(4))));

        Assert.Equal(ImportStatus.Restored, outcome.Status);
        Assert.Equal(2, outcome.Result!.Coffees);
        Assert.Equal(3, outcome.Result.Reviews);
        Assert.True(_store.Replaced);
    }

    [Fact]
    public async Task Warnings_from_the_store_reach_the_caller()
    {
        _store.Result = new ReplaceResult(1, 0, ["Unknown flavour tag \"Smoky\" was skipped."]);

        var outcome = await NewService().ImportAsync(Backup(Coffee()));

        Assert.Equal(ImportStatus.Restored, outcome.Status);
        Assert.Contains("Smoky", Assert.Single(outcome.Result!.Warnings));
    }

    [Fact]
    public async Task An_empty_backup_is_accepted_and_clears_the_catalog()
    {
        // Emptying the shelf deliberately is a legitimate thing to restore.
        var outcome = await NewService().ImportAsync(Backup());

        Assert.Equal(ImportStatus.Restored, outcome.Status);
        Assert.True(_store.Replaced);
    }

    private sealed class FakeBackupStore : IBackupStore
    {
        public IReadOnlyList<CoffeeWithReviews> Catalog { get; set; } = [];
        public ReplaceResult Result { get; set; } = new(0, 0, []);
        public bool Replaced { get; private set; }

        public Task<IReadOnlyList<CoffeeWithReviews>> ExportAsync(CancellationToken ct = default) =>
            Task.FromResult(Catalog);

        public Task<ReplaceResult> ReplaceAllAsync(
            IReadOnlyList<CoffeeWithReviews> coffees, CancellationToken ct = default)
        {
            Replaced = true;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
