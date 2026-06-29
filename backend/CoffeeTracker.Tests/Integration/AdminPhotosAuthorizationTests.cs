using System.Net;
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
}
