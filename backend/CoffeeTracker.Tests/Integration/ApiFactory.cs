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
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ct-it-{Guid.NewGuid():N}.db");
    private readonly string _photosPath = Directory.CreateTempSubdirectory("ct-it-photos-").FullName;
    private readonly string _logsPath = Directory.CreateTempSubdirectory("ct-it-logs-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={_dbPath}",
                ["Storage:PhotosPath"] = _photosPath,
                // Per-factory temp dir: without it every parallel WebApplicationFactory
                // host writes the same relative logs/coffee-<date>.log, contending on the
                // file lock (the sink is silently dropped) and littering a logs/ folder.
                ["FileLog:Directory"] = _logsPath,
                ["REGISTRATION_ENABLED"] = registrationEnabled ? "true" : "false",
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
        TryDelete(() => Directory.Delete(_photosPath, recursive: true));
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
