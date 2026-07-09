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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Required: lets Identity configure its own entity mappings before ours.
        base.OnModelCreating(builder);

        // Store the roast band as its name ("Light"/"Medium"/"Dark") rather than an int,
        // so the column stays human-readable and matches the API/JSON contract.
        builder.Entity<Coffee>()
            .Property(c => c.RoastLevel)
            .HasConversion<string>()
            .HasMaxLength(10);

        // A user may rate a coffee many times over its life — multiple entries per
        // (CoffeeId, UserId) are allowed. Keep a non-unique index so listing a
        // coffee's reviews and a user's entries for it stay fast.
        builder.Entity<Review>()
            .HasIndex(r => new { r.CoffeeId, r.UserId });

        // Optional context label for when the rating was taken.
        builder.Entity<Review>()
            .Property(r => r.Stage)
            .HasMaxLength(40);

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

        builder.Entity<RefreshToken>(token =>
        {
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.UserId);
            token.Property(t => t.TokenHash).HasMaxLength(64);
            // Deleting a user revokes their refresh tokens.
            token.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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
