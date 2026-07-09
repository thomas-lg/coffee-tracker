using System.Net;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Xunit;

namespace CoffeeTracker.Tests.Integration;

// Boots the real API in-process and drives it over HTTP to prove the Admin
// authorization policy actually gates /api/admin/photos (covers the gap the unit
// tests can't: the policy is wired at the HTTP boundary, not in the service).
public sealed class AdminPhotosAuthorizationTests : IntegrationTest
{
    [Fact]
    public async Task Admin_photos_endpoint_enforces_the_admin_policy()
    {
        // The first registered user becomes admin; the second is a normal user.
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        var user = await Client.RegisterAsync("user@example.com", "User");
        Assert.True(admin.IsAdmin);
        Assert.False(user.IsAdmin);

        // Anonymous → 401 (no token).
        var anonymous = await Client.Get("/api/admin/photos");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Authenticated non-admin → 403 (fails the Admin policy).
        var asUser = await Client.Get("/api/admin/photos", user.Token);
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);

        // Admin → 200.
        var asAdmin = await Client.Get("/api/admin/photos", admin.Token);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task Admin_photos_delete_enforces_the_admin_policy()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");
        var user = await Client.RegisterAsync("user@example.com", "User");
        var body = new { paths = new[] { "photos/does-not-exist.jpg" } };

        // Anonymous → 401 (no token).
        var anonymous = await Client.Delete("/api/admin/photos", token: null, body);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Authenticated non-admin → 403 (fails the Admin policy).
        var asUser = await Client.Delete("/api/admin/photos", user.Token, body);
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);

        // Admin → 200 (the unknown path is skipped, not an error).
        var asAdmin = await Client.Delete("/api/admin/photos", admin.Token, body);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
        var result = await asAdmin.Content.ReadFromJsonAsync<PhotoDeleteResultDto>();
        Assert.Equal(0, result!.Deleted);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public async Task Admin_photos_delete_with_no_paths_is_bad_request()
    {
        var admin = await Client.RegisterAsync("admin@example.com", "Admin");

        var res = await Client.Delete("/api/admin/photos", admin.Token, new { paths = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
