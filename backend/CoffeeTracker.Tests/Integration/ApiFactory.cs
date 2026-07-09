using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CoffeeTracker.Tests.Integration;

// Boots the real API in-process (WebApplicationFactory) against a throwaway SQLite
// database + photos directory, so the e2e suite drives the full HTTP stack —
// routing, model validation, auth, EF Core, migrations — without touching real
// data. Runs in Development so the JWT signing key is auto-generated and OCR is
// off; registration can be toggled to exercise the REGISTRATION_ENABLED gate.
public sealed class ApiFactory(bool registrationEnabled = true) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Known signing key (48 bytes, above the HS256 minimum) so security tests can
    /// mint their own tokens — correctly or deliberately malformed — and prove the
    /// bearer validation rejects the bad ones.
    /// </summary>
    public const string JwtKey = "integration-test-signing-key-integration-test-si";

    // Program.cs reads Jwt:Key and Storage:PhotosPath at top-level (to build
    // TokenValidationParameters and the /photos static-file root) BEFORE the
    // factory's in-memory configuration is applied — only the process environment
    // is visible at that point. Without these env vars, validation would use an
    // ephemeral dev key (401s for every issued token) and /photos would serve from
    // a different directory than the storage adapter writes to. The photos dir is
    // process-wide (per-factory isolation is impossible for a startup-read value);
    // server-generated GUID filenames keep parallel tests collision-free.
    private static readonly string SharedPhotosPath = Directory.CreateTempSubdirectory("ct-it-photos-").FullName;

    static ApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Storage__PhotosPath", SharedPhotosPath);
    }


    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ct-it-{Guid.NewGuid():N}.db");
    private readonly string _logsPath = Directory.CreateTempSubdirectory("ct-it-logs-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Storage:PhotosPath"] = SharedPhotosPath,
                // Per-factory temp dir: without it every parallel WebApplicationFactory
                // host writes the same relative logs/coffee-<date>.log, contending on the
                // file lock (the sink is silently dropped) and littering a logs/ folder.
                ["FileLog:Directory"] = _logsPath,
                ["REGISTRATION_ENABLED"] = registrationEnabled ? "true" : "false",
                ["Jwt:Key"] = JwtKey,
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        // WAL mode leaves -wal/-shm sidecar files next to the db; clean all three.
        TryDelete(() => File.Delete(_dbPath));
        TryDelete(() => File.Delete(_dbPath + "-wal"));
        TryDelete(() => File.Delete(_dbPath + "-shm"));
        // SharedPhotosPath is intentionally NOT deleted here: it is shared by every
        // factory in the process (see the static field) and parallel tests may still
        // be serving from it. It lives under the OS temp dir.
        TryDelete(() => Directory.Delete(_logsPath, recursive: true));
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (IOException)
        {
            // Best effort — a leftover temp file/dir is harmless.
        }
    }
}
