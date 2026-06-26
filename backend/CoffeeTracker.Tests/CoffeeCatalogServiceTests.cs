using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests;

// Exercises the application service against fakes — no database, EF Core, or
// filesystem involved. This is only possible because persistence and photo
// storage sit behind ports (the architectural boundary the hexagon enforces).
public class CoffeeCatalogServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 6, 26, 10, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryCoffeeRepository : ICoffeeRepository
    {
        private readonly Dictionary<int, Coffee> _store = new();
        private int _nextId = 1;

        /// <summary>When set, UpdateAsync throws — to exercise failure paths.</summary>
        public bool ThrowOnUpdate { get; set; }

        public InMemoryCoffeeRepository(params Coffee[] seed)
        {
            foreach (var c in seed)
            {
                _store[c.Id] = c;
                _nextId = Math.Max(_nextId, c.Id + 1);
            }
        }

        public Task<IReadOnlyList<Coffee>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Coffee>>(
                _store.Values.OrderByDescending(c => c.Id).ToList());

        public Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

        public Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default)
        {
            coffee.Id = _nextId++;
            _store[coffee.Id] = coffee;
            return Task.FromResult(coffee);
        }

        public Task UpdateAsync(Coffee coffee, CancellationToken ct = default)
        {
            if (ThrowOnUpdate)
            {
                throw new InvalidOperationException("simulated update failure");
            }

            _store[coffee.Id] = coffee;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_store.Remove(id));
    }

    private sealed class FakePhotoStorage(PhotoStorageResult result) : IPhotoStorage
    {
        public int SaveCalls { get; private set; }
        public List<string> Deleted { get; } = [];

        public Task<PhotoStorageResult> SaveAsync(Stream content, string? contentType, long length, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(result);
        }

        public Task DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            Deleted.Add(relativePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCurrentUser(string? id) : ICurrentUser
    {
        public string? Id { get; } = id;
    }

    private static CoffeeCatalogService NewService(
        ICoffeeRepository repo,
        IPhotoStorage? storage = null,
        string? currentUserId = null)
        => new(
            repo,
            storage ?? new FakePhotoStorage(PhotoStorageResult.Stored("photos/x.jpg")),
            new FakeCurrentUser(currentUserId),
            new FixedTimeProvider(FixedNow));

    private static Coffee SampleCoffee(int id = 7) => new()
    {
        Id = id,
        Name = "Yirgacheffe",
        Roaster = "Square Mile",
        Origin = "Ethiopia",
        RoastLevel = "Light",
        Price = 18.5m,
        DateBought = new DateOnly(2026, 6, 20),
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private static CoffeeCreateDto SampleCreateDto() => new(
        Name: "Yirgacheffe",
        Roaster: "Square Mile",
        Origin: "Ethiopia",
        RoastLevel: "Light",
        Price: 18.5m,
        DateBought: new DateOnly(2026, 6, 20),
        ShopName: "Local Roastery",
        PurchaseUrl: "https://example.com/coffee");

    [Fact]
    public async Task GetCatalogAsync_MapsDomainEntitiesToDtos()
    {
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()));

        var result = await service.GetCatalogAsync();

        var dto = Assert.Single(result);
        Assert.Equal(7, dto.Id);
        Assert.Equal("Yirgacheffe", dto.Name);
        Assert.Equal("Square Mile", dto.Roaster);
        Assert.Equal(18.5m, dto.Price);
        Assert.Null(dto.PhotoPath);
    }

    [Fact]
    public async Task GetCatalogAsync_ReturnsEmpty_WhenNoCoffees()
    {
        var service = NewService(new InMemoryCoffeeRepository());

        var result = await service.GetCatalogAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        var service = NewService(new InMemoryCoffeeRepository());

        Assert.Null(await service.GetByIdAsync(99));
    }

    [Fact]
    public async Task CreateAsync_AssignsId_StampsCreatedAt_AndMapsFields()
    {
        var service = NewService(new InMemoryCoffeeRepository());

        var created = await service.CreateAsync(SampleCreateDto());

        Assert.True(created.Id > 0);
        Assert.Equal(FixedNow, created.CreatedAt);
        Assert.Equal("Yirgacheffe", created.Name);
        Assert.Equal("Local Roastery", created.ShopName);
        Assert.Equal("https://example.com/coffee", created.PurchaseUrl);
        Assert.Null(created.PhotoPath);
    }

    [Fact]
    public async Task CreateAsync_StampsCurrentUserAsCreator()
    {
        var repo = new InMemoryCoffeeRepository();
        var service = NewService(repo, currentUserId: "user-123");

        var created = await service.CreateAsync(SampleCreateDto());

        var stored = await repo.GetByIdAsync(created.Id);
        Assert.Equal("user-123", stored!.CreatedByUserId);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesFields_WhenPresent()
    {
        var repo = new InMemoryCoffeeRepository(SampleCoffee());
        var service = NewService(repo);
        var dto = SampleCreateDto() with { Name = "Updated Name", Price = 22m };

        var found = await service.UpdateAsync(7, new CoffeeUpdateDto(
            dto.Name, dto.Roaster, dto.Origin, dto.RoastLevel, dto.Price,
            dto.DateBought, dto.ShopName, dto.PurchaseUrl));

        Assert.True(found);
        var updated = await service.GetByIdAsync(7);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal(22m, updated.Price);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalse_WhenMissing()
    {
        var service = NewService(new InMemoryCoffeeRepository());

        var found = await service.UpdateAsync(99, new CoffeeUpdateDto(
            "n", "r", "o", "Light", 1m, new DateOnly(2026, 1, 1), null, null));

        Assert.False(found);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCoffee_WhenPresent()
    {
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()));

        Assert.True(await service.DeleteAsync(7));
        Assert.Null(await service.GetByIdAsync(7));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenMissing()
    {
        var service = NewService(new InMemoryCoffeeRepository());

        Assert.False(await service.DeleteAsync(99));
    }

    [Fact]
    public async Task DeleteAsync_RemovesPhotoFile_WhenCoffeeHasOne()
    {
        var coffee = SampleCoffee();
        coffee.PhotoPath = "photos/old.jpg";
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/x.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(coffee), storage);

        await service.DeleteAsync(7);

        Assert.Equal(["photos/old.jpg"], storage.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotTouchStorage_WhenNoPhoto()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/x.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()), storage);

        await service.DeleteAsync(7);

        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task SetPhotoAsync_ReturnsNotFound_AndSkipsStorage_WhenCoffeeMissing()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/x.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(), storage);

        var result = await service.SetPhotoAsync(99, Stream.Null, "image/jpeg", 10);

        Assert.Equal(SetPhotoStatus.CoffeeNotFound, result.Status);
        Assert.Null(result.Coffee);
        Assert.Equal(0, storage.SaveCalls);
    }

    [Fact]
    public async Task SetPhotoAsync_SetsPhotoPath_OnSuccess()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/abc.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()), storage);

        var result = await service.SetPhotoAsync(7, Stream.Null, "image/jpeg", 10);

        Assert.Equal(SetPhotoStatus.Success, result.Status);
        Assert.Equal("photos/abc.jpg", result.Coffee!.PhotoPath);
        var reloaded = await service.GetByIdAsync(7);
        Assert.Equal("photos/abc.jpg", reloaded!.PhotoPath);
    }

    [Fact]
    public async Task SetPhotoAsync_DeletesPreviousPhoto_WhenReplacing()
    {
        var coffee = SampleCoffee();
        coffee.PhotoPath = "photos/old.jpg";
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/new.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(coffee), storage);

        await service.SetPhotoAsync(7, Stream.Null, "image/jpeg", 10);

        Assert.Equal(["photos/old.jpg"], storage.Deleted);
    }

    [Fact]
    public async Task SetPhotoAsync_DeletesNothing_OnFirstPhoto()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/new.jpg"));
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()), storage);

        await service.SetPhotoAsync(7, Stream.Null, "image/jpeg", 10);

        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task SetPhotoAsync_DeletesNewFile_WhenPersistFails()
    {
        var repo = new InMemoryCoffeeRepository(SampleCoffee()) { ThrowOnUpdate = true };
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/new.jpg"));
        var service = NewService(repo, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetPhotoAsync(7, Stream.Null, "image/jpeg", 10));

        // The just-stored file must not be left orphaned when the DB update fails.
        Assert.Equal(["photos/new.jpg"], storage.Deleted);
    }

    [Theory]
    [InlineData(PhotoStorageStatus.InvalidContentType, SetPhotoStatus.InvalidContentType)]
    [InlineData(PhotoStorageStatus.TooLarge, SetPhotoStatus.TooLarge)]
    public async Task SetPhotoAsync_PropagatesRejection_AndLeavesPhotoPathUnset(
        PhotoStorageStatus storageStatus, SetPhotoStatus expected)
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Rejected(storageStatus));
        var service = NewService(new InMemoryCoffeeRepository(SampleCoffee()), storage);

        var result = await service.SetPhotoAsync(7, Stream.Null, "text/plain", 10);

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Coffee);
        var reloaded = await service.GetByIdAsync(7);
        Assert.Null(reloaded!.PhotoPath);
    }
}
