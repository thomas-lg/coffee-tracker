using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driven;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// The scanning endpoint over real HTTP. Two things only an end-to-end test can show:
// the admin policy actually gates it, and the engine crosses the wire as a name rather
// than an ordinal, which is what stops a reordered enum repointing every client.
public sealed class AdminScanSettingsTests : IntegrationTest
{
    [Fact]
    public async Task Scan_settings_endpoints_enforce_the_admin_policy()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        var user = await Client.RegisterAsync("user@example.com", "User");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Get("/api/admin/scan-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.Get("/api/admin/scan-settings", user.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.Get("/api/admin/scan-settings", admin.Token)).StatusCode);

        var body = new UpdateScanSettingsDto(OcrEngine.Tesseract);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.Put("/api/admin/scan-settings", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.Put("/api/admin/scan-settings", body, user.Token)).StatusCode);
    }

    [Fact]
    public async Task An_admin_can_read_the_engine_and_every_option()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        var res = await Client.Get("/api/admin/scan-settings", admin.Token);
        var settings = await res.Content.ReadFromJsonAsync<ScanSettingsDto>();

        Assert.NotNull(settings);
        // Every engine the build knows, so the screen can disable what this host lacks
        // rather than offering a choice that answers 503 to every scan.
        Assert.Equal(3, settings.Options.Count);
        Assert.Contains(settings.Options, o => o.Engine == OcrEngine.RapidOcr);
        Assert.Contains(settings.Options, o => o.Engine == OcrEngine.Tesseract);
        Assert.Contains(settings.Options, o => o.Engine == OcrEngine.Disabled);
    }

    [Fact]
    public async Task A_chosen_engine_is_stored_and_read_back()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        var put = await Client.Put(
            "/api/admin/scan-settings", new UpdateScanSettingsDto(OcrEngine.Tesseract), admin.Token);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(OcrEngine.Tesseract, (await put.Content.ReadFromJsonAsync<ScanSettingsDto>())!.Engine);

        // Re-read on a fresh request, so the assertion is about what was stored rather
        // than what the update echoed.
        var get = await Client.Get("/api/admin/scan-settings", admin.Token);
        Assert.Equal(OcrEngine.Tesseract, (await get.Content.ReadFromJsonAsync<ScanSettingsDto>())!.Engine);
    }

    [Fact]
    public async Task The_engine_crosses_the_wire_as_a_name()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        await Client.Put("/api/admin/scan-settings", new UpdateScanSettingsDto(OcrEngine.Tesseract), admin.Token);

        var json = await (await Client.Get("/api/admin/scan-settings", admin.Token)).Content.ReadAsStringAsync();

        // Not "engine":1. An ordinal would mean reordering the enum silently repoints
        // every client at a different engine on the next deploy.
        Assert.Contains("\"engine\":\"Tesseract\"", json);
    }

    [Fact]
    public async Task A_request_that_names_no_engine_is_refused()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        // On a non-nullable enum a missing field binds to the first member, which would
        // silently switch the instance to RapidOcr. Nullable makes it a 400 instead.
        var res = await Client.Put("/api/admin/scan-settings", new { }, admin.Token);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
