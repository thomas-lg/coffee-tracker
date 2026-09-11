using CoffeeTracker.Application.Ports.Driven;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoffeeTracker.Tests.Integration;

// Boots the real API in-process (WebApplicationFactory) against a throwaway SQLite
// database + photos directory, so the e2e suite drives the full HTTP stack —
// routing, model validation, auth, EF Core, migrations — without touching real
// data. Runs in Development so the JWT signing key is auto-generated and OCR is
// off; registration can be toggled to exercise the local-account policy gate.
public sealed class ApiFactory(
    bool registrationEnabled = true,
    bool stampPolicy = true,
    IDictionary<string, string?>? oidc = null) : WebApplicationFactory<Program>
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
                ["Jwt:Key"] = JwtKey,
            }));

        // Provider settings, when a test needs one. Nulls are dropped so a test can
        // express "authority set, client id absent" and exercise the fail-fast.
        if (oidc is not null)
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(oidc.Where(kv => kv.Value is not null)));
        }
    }

    /// <summary>
    /// Stamps the account policy once the host is up, standing in for an administrator
    /// who set it from the admin view. Going through the policy (rather than the legacy
    /// REGISTRATION_ENABLED seed) matters: registration opened deliberately stays open,
    /// whereas the empty-instance bootstrap closes itself after the first account — so a
    /// suite that registers several users would otherwise be gated after the first.
    ///
    /// Pass <c>stampPolicy: false</c> to leave the app's own seeding in charge, which is
    /// what the bootstrap tests need.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        if (stampPolicy)
        {
            SetPolicy(host, new AccountPolicy(LocalLoginEnabled: true, LocalRegistrationEnabled: registrationEnabled));
        }

        return host;
    }

    /// <summary>Sets the policy mid-test, standing in for an admin changing it.</summary>
    public Task SetPolicyAsync(AccountPolicy policy)
    {
        SetPolicy(Services, policy);
        return Task.CompletedTask;
    }

    /// <summary>Reads the policy back, to assert on what the app actually persisted.</summary>
    public async Task<AccountPolicy> GetPolicyAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAccountPolicy>().GetAsync();
    }

    private static void SetPolicy(IHost host, AccountPolicy policy) => SetPolicy(host.Services, policy);

    private static void SetPolicy(IServiceProvider services, AccountPolicy policy)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IAccountPolicy>()
            .SetAsync(policy)
            .GetAwaiter()
            .GetResult();
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
