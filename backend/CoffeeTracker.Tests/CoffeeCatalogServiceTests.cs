using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests;

// Exercises the application service against a fake repository — no database or
// EF Core involved. This is only possible because data access sits behind the
// ICoffeeRepository port (the architectural boundary the refactor enforces).
public class CoffeeCatalogServiceTests
{
    private sealed class FakeCoffeeRepository(IReadOnlyList<Coffee> coffees) : ICoffeeRepository
    {
        public Task<IReadOnlyList<Coffee>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(coffees);
    }

    [Fact]
    public async Task GetCatalogAsync_MapsDomainEntitiesToDtos()
    {
        var repo = new FakeCoffeeRepository(
        [
            new Coffee
            {
                Id = 7,
                Name = "Yirgacheffe",
                Roaster = "Square Mile",
                Origin = "Ethiopia",
                RoastLevel = "Light",
                Price = 18.5m,
                DateBought = new DateOnly(2026, 6, 20),
                CreatedAt = DateTimeOffset.UnixEpoch,
            },
        ]);
        var service = new CoffeeCatalogService(repo);

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
        var service = new CoffeeCatalogService(new FakeCoffeeRepository([]));

        var result = await service.GetCatalogAsync();

        Assert.Empty(result);
    }
}
