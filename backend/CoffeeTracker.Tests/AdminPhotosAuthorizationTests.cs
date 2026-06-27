using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CoffeeTracker.Tests;

// Boots the real API in-process and drives it over HTTP to prove the Admin
// authorization policy actually gates /api/admin/photos (covers the gap the unit
// tests can't: the policy is wired at the HTTP boundary, not in the service).
public sealed class AdminPhotosAuthorizationTests : IClassFixture<AdminPhotosAuthorizationTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminPhotosAuthorizationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_photos_endpoint_enforces_the_admin_policy()
    {
        var client = _factory.CreateClient();

        // The first registered user becomes admin; the second is a normal user.
        var admin = await Register(client, "admin@example.com", "Admin");
        var user = await Register(client, "user@example.com", "User");
        Assert.True(admin.IsAdmin);
        Assert.False(user.IsAdmin);

        // Anonymous → 401 (no token).
        var anonymous = await client.GetAsync("/api/admin/photos");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Authenticated non-admin → 403 (fails the Admin policy).
        var asUser = await Get(client, "/api/admin/photos", user.Token);
        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);

        // Admin → 200.
        var asAdmin = await Get(client, "/api/admin/photos", admin.Token);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    private static async Task<AuthResponseDto> Register(HttpClient client, string email, string displayName)
    {
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto(email, "Sup3r-Secret!", displayName));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(req);
    }

    // Runs the app in Development (ephemeral JWT key, OCR off, registration on) with a
    // throwaway SQLite DB + photos dir so the suite never touches real data.
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ct-it-{Guid.NewGuid():N}.db");
        private readonly string _photosPath = Directory.CreateTempSubdirectory("ct-it-photos-").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                    ["Storage:PhotosPath"] = _photosPath,
                    ["REGISTRATION_ENABLED"] = "true",
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best effort — a temp file left behind is harmless.
            }

            try
            {
                Directory.Delete(_photosPath, recursive: true);
            }
            catch (IOException)
            {
                // Best effort.
            }
        }
    }
}
