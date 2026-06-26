using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests;

// Exercises the review service against fakes — enforces the one-per-user rule and
// ownership (owner-only edit; owner-or-admin delete) with no database involved.
public class ReviewServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 26, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCurrentUser(string? id, bool isAdmin = false) : ICurrentUser
    {
        public string? Id { get; } = id;
        public bool IsAdmin { get; } = isAdmin;
    }

    private sealed class FakeCoffeeRepo(params int[] existingIds) : ICoffeeRepository
    {
        private readonly HashSet<int> _ids = [.. existingIds];
        public Task<bool> ExistsAsync(int id, CancellationToken ct = default) => Task.FromResult(_ids.Contains(id));

        // Unused by ReviewService:
        public Task<IReadOnlyList<CoffeeWithStats>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<CoffeeWithStats?> GetWithStatsByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Coffee coffee, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeFlavorTagRepo : IFlavorTagRepository
    {
        private readonly Dictionary<int, FlavorTag> _tags = new()
        {
            [1] = new FlavorTag { Id = 1, Name = "Fruity" },
            [2] = new FlavorTag { Id = 2, Name = "Nutty" },
        };

        public Task<IReadOnlyList<FlavorTag>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FlavorTag>>(_tags.Values.OrderBy(t => t.Name).ToList());

        public Task<IReadOnlyList<FlavorTag>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FlavorTag>>(ids.Where(_tags.ContainsKey).Select(i => _tags[i]).ToList());
    }

    private sealed class FakeReviewRepo : IReviewRepository
    {
        private readonly Dictionary<int, Review> _store = new();
        private int _nextId = 1;

        public FakeReviewRepo(params Review[] seed)
        {
            foreach (var r in seed)
            {
                _store[r.Id] = r;
                _nextId = Math.Max(_nextId, r.Id + 1);
            }
        }

        public int Count => _store.Count;

        public Task<IReadOnlyList<Review>> GetByCoffeeAsync(int coffeeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Review>>(_store.Values.Where(r => r.CoffeeId == coffeeId).ToList());

        public Task<Review?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var r) ? r : null);

        public Task<bool> ExistsForUserAsync(int coffeeId, string userId, CancellationToken ct = default)
            => Task.FromResult(_store.Values.Any(r => r.CoffeeId == coffeeId && r.UserId == userId));

        public Task<Review> AddAsync(Review review, CancellationToken ct = default)
        {
            review.Id = _nextId++;
            _store[review.Id] = review;
            return Task.FromResult(review);
        }

        public Task UpdateAsync(Review review, CancellationToken ct = default)
        {
            _store[review.Id] = review;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Review review, CancellationToken ct = default)
        {
            _store.Remove(review.Id);
            return Task.CompletedTask;
        }
    }

    private static ReviewService NewService(
        FakeReviewRepo reviews,
        string? currentUserId = "user-1",
        bool isAdmin = false,
        FakeCoffeeRepo? coffees = null)
        => new(
            reviews,
            new FakeFlavorTagRepo(),
            coffees ?? new FakeCoffeeRepo(42),
            new FakeCurrentUser(currentUserId, isAdmin),
            new FixedTimeProvider(FixedNow));

    private static ReviewCreateDto CreateDto(int rating = 4, params int[] tagIds) =>
        new(rating, "Bright and juicy", "V60", "Medium", "1:16", tagIds);

    private static Review OwnedReview(int id, int coffeeId, string userId) => new()
    {
        Id = id,
        CoffeeId = coffeeId,
        UserId = userId,
        Rating = 3,
        CreatedAt = FixedNow,
    };

    [Fact]
    public async Task CreateAsync_StampsOwnerTimestampAndTags()
    {
        var repo = new FakeReviewRepo();
        var service = NewService(repo, currentUserId: "user-1");

        var result = await service.CreateAsync(42, CreateDto(rating: 5, 1, 2));

        Assert.Equal(ReviewStatus.Success, result.Status);
        Assert.Equal("user-1", result.Review!.UserId);
        Assert.Equal(5, result.Review.Rating);
        Assert.Equal(FixedNow, result.Review.CreatedAt);
        Assert.Equal(["Fruity", "Nutty"], result.Review.Tags.Select(t => t.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task CreateAsync_ReturnsCoffeeNotFound_WhenCoffeeMissing()
    {
        var service = NewService(new FakeReviewRepo(), coffees: new FakeCoffeeRepo(/* none */));

        var result = await service.CreateAsync(999, CreateDto());

        Assert.Equal(ReviewStatus.CoffeeNotFound, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ReturnsAlreadyReviewed_OnSecondReviewBySameUser()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "user-1"));
        var service = NewService(repo, currentUserId: "user-1");

        var result = await service.CreateAsync(42, CreateDto());

        Assert.Equal(ReviewStatus.AlreadyReviewed, result.Status);
        Assert.Equal(1, repo.Count);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsForbidden_WhenNotOwner()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "owner"));
        var service = NewService(repo, currentUserId: "intruder");

        var result = await service.UpdateAsync(42, 1, new ReviewUpdateDto(2, null, null, null, null, null));

        Assert.Equal(ReviewStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_StampsUpdatedAt_ForOwner()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "user-1"));
        var service = NewService(repo, currentUserId: "user-1");

        var result = await service.UpdateAsync(42, 1, new ReviewUpdateDto(5, "Even better", null, null, null, [2]));

        Assert.Equal(ReviewStatus.Success, result.Status);
        Assert.Equal(5, result.Review!.Rating);
        Assert.Equal(FixedNow, result.Review.UpdatedAt);
        Assert.Equal(["Nutty"], result.Review.Tags.Select(t => t.Name));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsForbidden_ForNonOwnerNonAdmin()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "owner"));
        var service = NewService(repo, currentUserId: "intruder", isAdmin: false);

        Assert.Equal(ReviewStatus.Forbidden, await service.DeleteAsync(42, 1));
        Assert.Equal(1, repo.Count);
    }

    [Fact]
    public async Task DeleteAsync_AllowsAdmin_ToDeleteOthersReview()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "owner"));
        var service = NewService(repo, currentUserId: "admin", isAdmin: true);

        Assert.Equal(ReviewStatus.Success, await service.DeleteAsync(42, 1));
        Assert.Equal(0, repo.Count);
    }

    [Fact]
    public async Task DeleteAsync_AllowsOwner()
    {
        var repo = new FakeReviewRepo(OwnedReview(1, 42, "user-1"));
        var service = NewService(repo, currentUserId: "user-1");

        Assert.Equal(ReviewStatus.Success, await service.DeleteAsync(42, 1));
    }

    [Fact]
    public async Task GetForCoffeeAsync_ReturnsCoffeeNotFound_WhenMissing()
    {
        var service = NewService(new FakeReviewRepo(), coffees: new FakeCoffeeRepo());

        var result = await service.GetForCoffeeAsync(999);

        Assert.Equal(ReviewStatus.CoffeeNotFound, result.Status);
        Assert.Null(result.Reviews);
    }
}
