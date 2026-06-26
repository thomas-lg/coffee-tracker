namespace CoffeeTracker.Domain;

/// <summary>
/// A flavor descriptor (e.g. "Fruity", "Chocolatey") that can be attached to
/// reviews. The set is seeded and shared across all users.
/// </summary>
public class FlavorTag
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Reviews carrying this tag (many-to-many).</summary>
    public ICollection<Review> Reviews { get; set; } = [];
}
