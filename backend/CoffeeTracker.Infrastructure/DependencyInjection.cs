using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeTracker.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers persistence adapters (driven ports) backed by EF Core + SQLite.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("Default")));

        services.AddScoped<ICoffeeRepository, EfCoffeeRepository>();
        return services;
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
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }
}
