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
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<FlavorTag> FlavorTags => Set<FlavorTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Required: lets Identity configure its own entity mappings before ours.
        base.OnModelCreating(builder);

        // One review per user per coffee. The unique index is the source of truth
        // (a backstop against races); the service also pre-checks.
        builder.Entity<Review>()
            .HasIndex(r => new { r.CoffeeId, r.UserId })
            .IsUnique();

        // Deleting a coffee removes its reviews.
        builder.Entity<Review>()
            .HasOne<Coffee>()
            .WithMany()
            .HasForeignKey(r => r.CoffeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Review <-> FlavorTag: EF implicit many-to-many (auto join table).
        builder.Entity<Review>()
            .HasMany(r => r.Tags)
            .WithMany(t => t.Reviews);

        builder.Entity<FlavorTag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // Seed the starter tag set into the schema (idempotent via the migration).
        builder.Entity<FlavorTag>().HasData(
            new FlavorTag { Id = 1, Name = "Fruity" },
            new FlavorTag { Id = 2, Name = "Berry" },
            new FlavorTag { Id = 3, Name = "Citrus" },
            new FlavorTag { Id = 4, Name = "Floral" },
            new FlavorTag { Id = 5, Name = "Chocolatey" },
            new FlavorTag { Id = 6, Name = "Nutty" },
            new FlavorTag { Id = 7, Name = "Caramel" },
            new FlavorTag { Id = 8, Name = "Spicy" },
            new FlavorTag { Id = 9, Name = "Earthy" },
            new FlavorTag { Id = 10, Name = "Winey" });
    }
}
