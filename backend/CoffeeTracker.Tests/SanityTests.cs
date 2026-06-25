using Xunit;

namespace CoffeeTracker.Tests;

// Skeleton test project (M8). It stands alone today so `dotnet test` is green
// before any app code exists. As the milestones land, add a ProjectReference to
// CoffeeTracker.Api (see the csproj) and replace this sanity check with the
// representative tests called out in PLAN.md:
//   - CoffeeLabelParser regex parsing               (M5)
//   - auth / per-user review ownership rules          (M3/M4)
//   - DTO DataAnnotations validation                  (M2)
public class SanityTests
{
    [Fact]
    public void TestProject_IsWiredUp()
    {
        Assert.True(true);
    }
}
