using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// End-to-end reviews flow: ratings-over-time aggregation (average + count),
// flavour-tag aggregation (distinct, sorted), tag/coffee validation, and the
// ownership rules (owner-only edit; owner-or-admin delete).
public sealed class ReviewApiTests : IntegrationTest
{
    private static CoffeeCreateDto SampleCoffee() => new(
        Name: "House Blend",
        Roaster: "Test Roastery",
        Origin: "Blend",
        RoastLevel: RoastLevel.Medium,
        Price: 14.00m,
        DateBought: new DateOnly(2026, 1, 1),
        ShopName: null,
        PurchaseUrl: null);

    private async Task<int> CreateCoffeeAsync(string token)
    {
        var res = await Client.Post("/api/coffees", SampleCoffee(), token);
        res.EnsureSuccessStatusCode();
        var created = (await res.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        return created.Id;
    }

    private async Task<CoffeeResponseDto> GetCoffeeAsync(int id, string token) =>
        (await (await Client.Get($"/api/coffees/{id}", token)).Content.ReadFromJsonAsync<CoffeeResponseDto>())!;

    private async Task<IReadOnlyDictionary<string, int>> FlavorTagIdsByNameAsync(string token)
    {
        var res = await Client.Get("/api/flavor-tags", token);
        res.EnsureSuccessStatusCode();
        var tags = (await res.Content.ReadFromJsonAsync<List<FlavorTagDto>>())!;
        return tags.ToDictionary(t => t.Name, t => t.Id);
    }

    [Fact]
    public async Task Reviews_require_authentication()
    {
        var res = await Client.Get("/api/coffees/1/reviews");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Seeded_flavor_tags_are_listed()
    {
        var user = await Client.RegisterAsync("tags@example.com", "Tags");

        var tags = await FlavorTagIdsByNameAsync(user.Token);

        Assert.Contains("Fruity", tags.Keys);
        Assert.Contains("Chocolatey", tags.Keys);
        Assert.Contains("Citrus", tags.Keys);
    }

    [Fact]
    public async Task Ratings_over_time_aggregate_into_average_and_count()
    {
        var user = await Client.RegisterAsync("rater@example.com", "Rater");
        var coffeeId = await CreateCoffeeAsync(user.Token);

        // The same user rates the same coffee three times over its life — each POST
        // is a separate dated entry (no one-per-user limit).
        foreach (var (rating, stage) in new[] { (5, "Fresh bag"), (4, "Mid-week"), (3, "Last cups") })
        {
            var res = await Client.Post(
                $"/api/coffees/{coffeeId}/reviews",
                new ReviewCreateDto(rating, null, null, null, null, null, Stage: stage),
                user.Token);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        var reviewsRes = await Client.Get($"/api/coffees/{coffeeId}/reviews", user.Token);
        var reviews = (await reviewsRes.Content.ReadFromJsonAsync<List<ReviewResponseDto>>())!;
        Assert.Equal(3, reviews.Count);

        var coffee = await GetCoffeeAsync(coffeeId, user.Token);
        Assert.Equal(3, coffee.ReviewCount);
        Assert.NotNull(coffee.AverageRating);
        Assert.Equal(4.0, coffee.AverageRating!.Value, precision: 3); // (5 + 4 + 3) / 3
    }

    [Fact]
    public async Task Flavor_tags_aggregate_distinct_and_sorted_across_reviews()
    {
        var user = await Client.RegisterAsync("flavors@example.com", "Flavors");
        var tags = await FlavorTagIdsByNameAsync(user.Token);
        var coffeeId = await CreateCoffeeAsync(user.Token);

        // Two reviews sharing "Chocolatey"; the coffee's tag set should dedupe it and
        // come back sorted: Chocolatey, Citrus, Fruity.
        await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(5, null, null, null, null, new[] { tags["Chocolatey"], tags["Fruity"] }),
            user.Token);
        await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(4, null, null, null, null, new[] { tags["Chocolatey"], tags["Citrus"] }),
            user.Token);

        var coffee = await GetCoffeeAsync(coffeeId, user.Token);

        Assert.Equal(new[] { "Chocolatey", "Citrus", "Fruity" }, coffee.FlavorTags);
    }

    [Fact]
    public async Task Creating_a_review_with_an_unknown_tag_is_rejected()
    {
        var user = await Client.RegisterAsync("badtag@example.com", "Bad Tag");
        var coffeeId = await CreateCoffeeAsync(user.Token);

        var res = await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(4, null, null, null, null, new[] { 999_999 }),
            user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Creating_a_review_for_a_missing_coffee_is_not_found()
    {
        var user = await Client.RegisterAsync("ghost@example.com", "Ghost");

        var res = await Client.Post(
            "/api/coffees/999999/reviews",
            new ReviewCreateDto(4, null, null, null, null, null),
            user.Token);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Editing_or_deleting_another_users_review_is_forbidden()
    {
        var owner = await Client.RegisterAsync("owner-review@example.com", "Owner"); // first → admin
        var other = await Client.RegisterAsync("other-review@example.com", "Other"); // non-admin
        var coffeeId = await CreateCoffeeAsync(owner.Token);

        var createRes = await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(5, "Owner's note", null, null, null, null),
            owner.Token);
        var review = (await createRes.Content.ReadFromJsonAsync<ReviewResponseDto>())!;

        // A non-admin, non-owner can neither rewrite nor remove someone else's review.
        var editRes = await Client.Put(
            $"/api/coffees/{coffeeId}/reviews/{review.Id}",
            new ReviewUpdateDto(1, "Hijacked", null, null, null, null),
            other.Token);
        Assert.Equal(HttpStatusCode.Forbidden, editRes.StatusCode);

        var deleteRes = await Client.Delete($"/api/coffees/{coffeeId}/reviews/{review.Id}", other.Token);
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task Owner_can_delete_own_review_and_admin_can_moderate_others()
    {
        var admin = await Client.RegisterAsync("admin-review@example.com", "Admin"); // first → admin
        var member = await Client.RegisterAsync("member-review@example.com", "Member"); // non-admin
        var coffeeId = await CreateCoffeeAsync(admin.Token);

        // Owner deletes their own review.
        var ownReview = (await (await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(4, null, null, null, null, null),
            member.Token)).Content.ReadFromJsonAsync<ReviewResponseDto>())!;
        var ownDelete = await Client.Delete($"/api/coffees/{coffeeId}/reviews/{ownReview.Id}", member.Token);
        Assert.Equal(HttpStatusCode.NoContent, ownDelete.StatusCode);

        // Admin moderates (deletes) a member's review.
        var membersReview = (await (await Client.Post(
            $"/api/coffees/{coffeeId}/reviews",
            new ReviewCreateDto(2, null, null, null, null, null),
            member.Token)).Content.ReadFromJsonAsync<ReviewResponseDto>())!;
        var adminDelete = await Client.Delete($"/api/coffees/{coffeeId}/reviews/{membersReview.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.NoContent, adminDelete.StatusCode);

        var remaining = (await (await Client.Get($"/api/coffees/{coffeeId}/reviews", admin.Token))
            .Content.ReadFromJsonAsync<List<ReviewResponseDto>>())!;
        Assert.Empty(remaining);
    }
}
