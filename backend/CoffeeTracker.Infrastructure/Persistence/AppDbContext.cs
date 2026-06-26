using CoffeeTracker.Domain;
using CoffeeTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// EF Core context. Derives from <see cref="IdentityDbContext{TUser}"/> so the
/// ASP.NET Identity tables (AspNetUsers, AspNetRoles, …) live alongside the app's
/// own tables in the same SQLite database.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Coffee> Coffees => Set<Coffee>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Required: lets Identity configure its own entity mappings before ours.
        base.OnModelCreating(builder);
    }
}
