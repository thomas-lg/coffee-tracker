using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// End-to-end coffee catalog flow: the authenticated CRUD round-trip and the
// model-validation rejections — including the roast-level regression where an
// omitted roastLevel must 400 rather than silently default to Light.
public sealed class CoffeeApiTests : IntegrationTest
{
    private static CoffeeCreateDto SampleCoffee() => new(
        Name: "Yirgacheffe",
        Roaster: "Blue Bottle",
        Origin: "Ethiopia",
        RoastLevel: RoastLevel.Light,
        Price: 18.50m,
        DateBought: new DateOnly(2026, 1, 15),
        ShopName: "Local Roastery",
        PurchaseUrl: "https://example.com/yirgacheffe");

    [Fact]
    public async Task Catalog_requires_authentication()
    {
        // Reads included — an account is mandatory to use the app at all.
        var res = await Client.Get("/api/coffees");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Create_read_update_delete_round_trip()
    {
        var user = await Client.RegisterAsync("owner@example.com", "Owner");

        // Create → 201 with the freshly created coffee.
        var createRes = await Client.Post("/api/coffees", SampleCoffee(), user.Token);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        Assert.True(created.Id > 0);
        Assert.Equal("Yirgacheffe", created.Name);
        Assert.Equal(RoastLevel.Light, created.RoastLevel);
        Assert.Null(created.AverageRating);
        Assert.Equal(0, created.ReviewCount);
        Assert.Empty(created.FlavorTags);

        // Get one → matches.
        var getRes = await Client.Get($"/api/coffees/{created.Id}", user.Token);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = (await getRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        Assert.Equal(created.Id, fetched.Id);

        // List → contains it.
        var listRes = await Client.Get("/api/coffees", user.Token);
        var list = (await listRes.Content.ReadFromJsonAsync<List<CoffeeResponseDto>>())!;
        Assert.Contains(list, c => c.Id == created.Id);

        // Update → 204, then the change is visible.
        var update = SampleCoffee() with { Name = "Yirgacheffe (Natural)", RoastLevel = RoastLevel.Medium };
        var updateRes = await Client.Put($"/api/coffees/{created.Id}", update, user.Token);
        Assert.Equal(HttpStatusCode.NoContent, updateRes.StatusCode);
        var afterUpdate = (await (await Client.Get($"/api/coffees/{created.Id}", user.Token)).Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        Assert.Equal("Yirgacheffe (Natural)", afterUpdate.Name);
        Assert.Equal(RoastLevel.Medium, afterUpdate.RoastLevel);

        // Delete → 204, then it's gone.
        var deleteRes = await Client.Delete($"/api/coffees/{created.Id}", user.Token);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        var afterDelete = await Client.Get($"/api/coffees/{created.Id}", user.Token);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Creating_a_coffee_without_a_roast_level_is_rejected()
    {
        // Regression guard: roastLevel is [Required]; omitting it from the JSON body
        // must 400, not bind to the enum's default (Light).
        var user = await Client.RegisterAsync("noroast@example.com", "No Roast");
        var body = new
        {
            name = "Mystery Roast",
            roaster = "Anon",
            origin = "Somewhere",
            price = 12.00m,
            dateBought = "2026-01-10",
        };

        var res = await Client.Post("/api/coffees", body, user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Creating_a_coffee_with_an_invalid_roast_level_is_rejected()
    {
        var user = await Client.RegisterAsync("badroast@example.com", "Bad Roast");
        var body = new
        {
            name = "Charred",
            roaster = "Anon",
            origin = "Somewhere",
            roastLevel = "Charred",
            price = 12.00m,
            dateBought = "2026-01-10",
        };

        var res = await Client.Post("/api/coffees", body, user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Creating_a_coffee_with_a_negative_price_is_rejected()
    {
        var user = await Client.RegisterAsync("cheapskate@example.com", "Cheapskate");
        var body = SampleCoffee() with { Price = -1m };

        var res = await Client.Post("/api/coffees", body, user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
