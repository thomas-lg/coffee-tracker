using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// HTTP-boundary coverage for the two file-upload endpoints: authorization (401),
// the empty-file guard (400), the content-type allowlist + magic-byte sniff (400),
// and the happy path (200). OCR is disabled in the test environment, so the scan
// endpoint surfaces its OcrUnavailable (503) path.
public sealed class UploadEndpointTests : IntegrationTest
{
    private static CoffeeCreateDto SampleCoffee() => new(
        Name: "Yirgacheffe",
        Roaster: "Blue Bottle",
        Origin: "Ethiopia",
        RoastLevel: RoastLevel.Light,
        Price: 18.50m,
        DateBought: new DateOnly(2026, 1, 15),
        ShopName: null,
        PurchaseUrl: null);

    private async Task<(string Token, int CoffeeId)> CreateOwnedCoffeeAsync()
    {
        var owner = await Client.RegisterAsync("owner@example.com", "Owner");
        var createRes = await Client.Post("/api/coffees", SampleCoffee(), owner.Token);
        var coffee = (await createRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        return (owner.Token, coffee.Id);
    }

    [Fact]
    public async Task Scan_requires_authentication()
    {
        var res = await Client.PostFile("/api/coffees/scan", ApiClient.RealPng(), "image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Photo_upload_requires_authentication()
    {
        var res = await Client.PostFile("/api/coffees/1/photo", ApiClient.RealPng(), "image/png");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Scan_with_an_empty_file_is_rejected()
    {
        var user = await Client.RegisterAsync("scanner@example.com", "Scanner");

        var res = await Client.PostFile("/api/coffees/scan", [], "image/png", user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Scan_reports_503_when_ocr_is_unavailable()
    {
        // The test host runs with Ocr:Engine=none, so a well-formed scan degrades to 503.
        var user = await Client.RegisterAsync("scanner2@example.com", "Scanner Two");

        var res = await Client.PostFile("/api/coffees/scan", ApiClient.RealPng(), "image/png", user.Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task Photo_upload_rejects_a_disallowed_content_type()
    {
        var (token, coffeeId) = await CreateOwnedCoffeeAsync();

        var res = await Client.PostFile($"/api/coffees/{coffeeId}/photo", "not an image"u8.ToArray(), "text/plain", token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Photo_upload_rejects_bytes_that_dont_match_the_declared_type()
    {
        var (token, coffeeId) = await CreateOwnedCoffeeAsync();

        // Claims image/png but the bytes are not a PNG — the magic-byte sniff must reject.
        var res = await Client.PostFile($"/api/coffees/{coffeeId}/photo", "totally not a png"u8.ToArray(), "image/png", token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Photo_upload_rejects_a_polyglot_that_sniffs_as_png_but_cannot_decode()
    {
        var (token, coffeeId) = await CreateOwnedCoffeeAsync();

        // Valid PNG magic number followed by garbage: passes the sniff, fails the
        // decode — the re-encoding pipeline must reject it, not store it.
        byte[] polyglot = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03, 0x04];
        var res = await Client.PostFile($"/api/coffees/{coffeeId}/photo", polyglot, "image/png", token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Photo_upload_succeeds_for_a_valid_image_from_the_owner()
    {
        var (token, coffeeId) = await CreateOwnedCoffeeAsync();

        var res = await Client.PostFile($"/api/coffees/{coffeeId}/photo", ApiClient.RealPng(), "image/png", token);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var coffee = (await res.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;
        Assert.False(string.IsNullOrEmpty(coffee.PhotoUrl));
    }

    [Fact]
    public async Task Photo_upload_to_someone_elses_coffee_is_forbidden()
    {
        // First registered user is the instance admin; keep them out of the way so
        // both the owner and the intruder are ordinary users.
        await Client.RegisterAsync("bootstrap-admin@example.com", "Admin");
        var owner = await Client.RegisterAsync("photo-owner@example.com", "Owner");
        var intruder = await Client.RegisterAsync("intruder@example.com", "Intruder");

        var createRes = await Client.Post("/api/coffees", SampleCoffee(), owner.Token);
        var coffee = (await createRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;

        var res = await Client.PostFile($"/api/coffees/{coffee.Id}/photo", ApiClient.RealPng(), "image/png", intruder.Token);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        // The coffee must remain photo-less — the upload was rejected before storage.
        var reloaded = await (await Client.Get($"/api/coffees/{coffee.Id}", owner.Token)).Content.ReadFromJsonAsync<CoffeeResponseDto>();
        Assert.Null(reloaded!.PhotoUrl);
    }
}
