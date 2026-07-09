namespace CoffeeTracker.Domain;

/// <summary>
/// One dated rating of a coffee by a user: a rating plus optional stage, tasting
/// notes, brew details, and flavor tags. A user MAY rate the same coffee multiple
/// times over its life (fresh bag, mid-week, last cup) — each is a separate entry,
/// ordered by <see cref="CreatedAt"/>.
/// </summary>
public class Review
{
    public int Id { get; set; }

    public int CoffeeId { get; set; }

    /// <summary>Id of the user who wrote the review (from the auth token).</summary>
    public required string UserId { get; set; }

    /// <summary>1–5; validated at the API boundary.</summary>
    public int Rating { get; set; }

    /// <summary>
    /// Optional free-text context for when this rating was taken
    /// (e.g. "Fresh bag", "Mid-week", "Last cups").
    /// </summary>
    public string? Stage { get; set; }

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

    /// <summary>Only the author may edit their own review (admins may delete, not rewrite).</summary>
    public bool IsEditableBy(string userId) => UserId == userId;

    /// <summary>The author or an admin (moderation) may delete a review.</summary>
    public bool IsDeletableBy(string userId, bool isAdmin) => UserId == userId || isAdmin;
}
