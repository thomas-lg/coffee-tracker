namespace CoffeeTracker.Domain;

/// <summary>
/// One user's review of a coffee: a rating plus optional tasting notes, brew
/// details, and flavor tags. A user has at most one review per coffee (enforced
/// by a unique index in the persistence layer).
/// </summary>
public class Review
{
    public int Id { get; set; }

    public int CoffeeId { get; set; }

    /// <summary>Id of the user who wrote the review (from the auth token).</summary>
    public required string UserId { get; set; }

    /// <summary>1–5; validated at the API boundary.</summary>
    public int Rating { get; set; }

    public string? TastingNotes { get; set; }

    /// <summary>Free-text brew method (e.g. "V60", "Espresso").</summary>
    public string? BrewMethod { get; set; }

    /// <summary>Free-text grind setting (e.g. "Medium-fine").</summary>
    public string? Grind { get; set; }

    /// <summary>Free-text brew ratio (e.g. "1:16").</summary>
    public string? Ratio { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Flavor tags attached to this review (many-to-many).</summary>
    public ICollection<FlavorTag> Tags { get; set; } = [];
}
