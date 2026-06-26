using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoffeeTracker.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef` can build the context from the
/// Infrastructure project alone — the Api (startup) project stays free of any
/// EF Core / design-time dependency. Not used at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=coffee.db")
            .Options;
        return new AppDbContext(options);
    }
}
