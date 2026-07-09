namespace CoffeeTracker.Domain;

/// <summary>
/// A coffee a user has bought and wants to catalog. The domain entity is the
/// hexagon core — it carries no persistence or framework concerns.
/// </summary>
public class Coffee
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Roaster { get; set; }

    public required string Origin { get; set; }

    /// <summary>Roast band — a closed set (Light/Medium/Dark).</summary>
    public required RoastLevel RoastLevel { get; set; }

    public decimal Price { get; set; }

    public DateOnly DateBought { get; set; }

    /// <summary>Relative path to the bag photo, set in M2; null until a photo is uploaded.</summary>
    public string? PhotoPath { get; set; }

    public string? ShopName { get; set; }

    public string? PurchaseUrl { get; set; }

    /// <summary>Id of the user who created the record. Populated from the auth token in M3.</summary>
    public string? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Whether the given user may modify (edit/delete/replace photo) this coffee.
    /// Restricted to the creator or an admin; rows created before owner-stamping (null
    /// <see cref="CreatedByUserId"/>) are admin-only — never world-writable.
    /// </summary>
    public bool IsModifiableBy(string? userId, bool isAdmin) =>
        isAdmin || (CreatedByUserId is not null && CreatedByUserId == userId);
}
