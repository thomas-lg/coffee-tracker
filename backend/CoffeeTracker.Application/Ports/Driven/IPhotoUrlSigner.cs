namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Produces and validates time-limited, signed URLs for stored photos, so photo
/// files require a valid capability to fetch rather than being world-readable.
/// </summary>
public interface IPhotoUrlSigner
{
    /// <summary>
    /// Signs a stored relative path (e.g. <c>photos/ab12.jpg</c>) into a time-limited
    /// absolute URL (<c>/photos/ab12.jpg?exp=…&amp;sig=…</c>). Returns null for a null path.
    /// </summary>
    string? Sign(string? relativePath);

    /// <summary>Validates a photo request's file name against its expiry and signature.</summary>
    bool Validate(string fileName, string? exp, string? sig);
}
