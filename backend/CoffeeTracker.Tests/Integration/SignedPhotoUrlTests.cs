using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Domain;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// Photos are user data: a raw GET on /photos/<name> must be refused unless the URL
// carries a valid, unexpired signature (the capability embedded in coffee/scan
// responses). Drives the real middleware in Program.cs end to end: upload a photo,
// read back its signed URL, then fetch it with and without a valid signature.
public sealed class SignedPhotoUrlTests : IntegrationTest
{
    private static CoffeeCreateDto SampleCoffee() => new(
        Name: "Signed Photo",
        Roaster: "Roaster",
        Origin: "Origin",
        RoastLevel: RoastLevel.Medium,
        Price: 10m,
        DateBought: new DateOnly(2026, 1, 1),
        ShopName: null,
        PurchaseUrl: null);

    /// <summary>Creates a coffee with a photo and returns its signed photo URL.</summary>
    private async Task<string> UploadPhotoAsync()
    {
        var user = await Client.RegisterAsync("photos@example.com", "Photos");
        var createRes = await Client.Post("/api/coffees", SampleCoffee(), user.Token);
        var coffee = (await createRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;

        var uploadRes = await Client.PostFile($"/api/coffees/{coffee.Id}/photo", ApiClient.RealPng(), "image/png", user.Token);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);
        var updated = (await uploadRes.Content.ReadFromJsonAsync<CoffeeResponseDto>())!;

        Assert.False(string.IsNullOrEmpty(updated.PhotoUrl));
        Assert.Contains("exp=", updated.PhotoUrl);
        Assert.Contains("sig=", updated.PhotoUrl);
        return updated.PhotoUrl!;
    }

    [Fact]
    public async Task Signed_photo_url_is_served()
    {
        var signedUrl = await UploadPhotoAsync();

        // No bearer token: the signature in the URL is the capability.
        var res = await Client.Get(signedUrl);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
        Assert.True((await res.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Unsigned_photo_request_is_rejected()
    {
        var signedUrl = await UploadPhotoAsync();
        var bareUrl = signedUrl[..signedUrl.IndexOf('?')]; // strip exp+sig

        var res = await Client.Get(bareUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Tampered_signature_is_rejected()
    {
        var signedUrl = await UploadPhotoAsync();
        var sigStart = signedUrl.IndexOf("sig=", StringComparison.Ordinal) + 4;
        var tampered = signedUrl[..sigStart] + "AAAAtamperedAAAA";

        var res = await Client.Get(tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
