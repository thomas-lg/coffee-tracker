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
        services.AddSingleton<IPhotoStorage, FileSystemPhotoStorage>();

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
    /// JWTs, not cookies) and the auth driving-port adapter. JWT bearer *validation*
    /// is wired in the Api project (it owns the HTTP pipeline); token *issuance* and
    /// user management live here.
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
        // REGISTRATION_ENABLED is a flat env var / key (default off), per the deploy docs.
        services.Configure<RegistrationOptions>(o => o.Enabled = configuration.GetValue<bool>("REGISTRATION_ENABLED"));

        services.AddSingleton<TokenService>();
        services.AddScoped<IAuthService, IdentityAuthService>();
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
