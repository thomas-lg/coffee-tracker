using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Infrastructure.Ocr;
using CoffeeTracker.Infrastructure.Persistence;
using CoffeeTracker.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence adapters (driven ports) backed by EF Core + SQLite.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));

        services.Configure<PhotoStorageOptions>(configuration.GetSection(PhotoStorageOptions.SectionName));

        services.AddScoped<ICoffeeRepository, EfCoffeeRepository>();
        services.AddScoped<IReviewRepository, EfReviewRepository>();
        services.AddScoped<IFlavorTagRepository, EfFlavorTagRepository>();
        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();
        services.AddScoped<IAccountPolicy, EfAccountPolicy>();
        services.AddSingleton<IPhotoStorage, FileSystemPhotoStorage>();
        services.AddSingleton<IPhotoUrlSigner, PhotoUrlSigner>();

        AddOcr(services, configuration);
        AddAuth(services, configuration);
        return services;
    }

    /// <summary>
    /// Registers the OCR adapter selected by <c>Ocr:Engine</c>: <c>tesseract</c>
    /// (default — shells out to the system <c>tesseract</c> CLI) or <c>none</c>
    /// (disabled — for hosts without it).
    /// </summary>
    private static void AddOcr(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));

        var engine = configuration.GetValue<string>($"{OcrOptions.SectionName}:{nameof(OcrOptions.Engine)}");
        if (string.Equals(engine, "none", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IOcrService, DisabledOcrService>();
        }
        else
        {
            services.AddSingleton<IOcrService, TesseractCliOcrService>();
        }
    }

    /// <summary>
    /// Registers ASP.NET Identity (UserManager only — this API authenticates with
    /// JWTs, not cookies) and the auth driven-port adapters. JWT bearer *validation*
    /// is wired in the Api project (it owns the HTTP pipeline); the auth use case lives
    /// in the Application layer and drives these adapters (user store, token issuer,
    /// the refresh-token store and account policy are registered above).
    /// </summary>
    private static void AddAuth(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Password policy. Length is the main lever; the character-class
                // requirements are relaxed since a long passphrase is stronger.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;

                // Lockout: throttle brute force at the account level (rate limiting
                // throttles it at the endpoint level — see the Api project).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<ITokenIssuer, TokenService>();
        services.AddScoped<IUserDirectory, IdentityUserDirectory>();
        AddExternalIdentityProvider(services, configuration);
    }

    /// <summary>
    /// Registers the external identity provider: the real adapter when one is
    /// configured, otherwise a stand-in that reports itself unavailable.
    ///
    /// A half-configured provider is a startup failure rather than a dormant feature.
    /// Booting with an authority but no client id would leave a sign-in button that
    /// cannot possibly work, and the operator would learn about it from a user. This
    /// mirrors the stance already taken on a missing Jwt:Key.
    ///
    /// Both the validation and the choice of adapter are deferred to the options
    /// system rather than read here: configuration is not final at registration time
    /// (a host can still layer sources over it), so deciding eagerly would read a
    /// half-built configuration.
    /// </summary>
    private static void AddExternalIdentityProvider(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OidcOptions>()
            .Bind(configuration.GetSection(OidcOptions.SectionName))
            .Validate(
                o => string.IsNullOrWhiteSpace(o.Authority) == string.IsNullOrWhiteSpace(o.ClientId),
                $"{OidcOptions.SectionName}:{nameof(OidcOptions.Authority)} and {nameof(OidcOptions.ClientId)} " +
                "must be set together. Provide the missing one via its environment variable, or remove the whole " +
                $"{OidcOptions.SectionName} section to run without an identity provider.")
            .Validate(
                o => string.IsNullOrWhiteSpace(o.AdminClaim) == string.IsNullOrWhiteSpace(o.AdminClaimValue),
                $"{OidcOptions.SectionName}:{nameof(OidcOptions.AdminClaim)} and {nameof(OidcOptions.AdminClaimValue)} " +
                "must be set together. A claim with no value to match would grant administrator rights to anyone " +
                "carrying it.")
            .ValidateOnStart();

        services.AddSingleton<IExternalIdentityProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OidcOptions>>();
            return options.Value.IsConfigured
                ? ActivatorUtilities.CreateInstance<OidcIdentityProvider>(sp)
                : new UnconfiguredIdentityProvider();
        });
    }

    /// <summary>
    /// Applies any pending EF Core migrations. Lets the Api (startup) project
    /// initialize the database without referencing EF Core or the DbContext type.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        // REGISTRATION_ENABLED is legacy: read once, only to preserve the posture of a
        // deployment that predates the persisted policy. See AccountPolicySeeder.
        await AccountPolicySeeder.SeedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("REGISTRATION_ENABLED"),
            ct);

        // Switch SQLite to Write-Ahead Logging. Unlike the default rollback journal,
        // WAL lets readers proceed concurrently with a writer, which cuts down on
        // "database is locked" errors under our (light, single-instance) load.
        // journal_mode is persisted in the database file, so this only needs to run
        // once, but issuing it on every startup is cheap and idempotent.
        //
        // PRAGMA journal_mode returns the resulting mode instead of throwing when
        // it can't switch (e.g. the DB lives on a network filesystem that doesn't
        // support WAL's shared-memory index), so read the result back and warn
        // rather than silently believing WAL is on.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            var resultingMode = await command.ExecuteScalarAsync(ct) as string;

            if (!string.Equals(resultingMode, "wal", StringComparison.OrdinalIgnoreCase))
            {
                scope.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("CoffeeTracker.Infrastructure.Database")
                    .LogWarning(
                        "SQLite WAL mode was not enabled (journal_mode={JournalMode}); " +
                        "the database file may be on a filesystem that does not support WAL.",
                        resultingMode ?? "unknown");
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
