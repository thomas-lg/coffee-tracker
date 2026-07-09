namespace CoffeeTracker.Infrastructure.Storage;

/// <summary>
/// Configuration for where and how uploaded photos are stored. Bound from the
/// <c>Storage</c> configuration section.
/// </summary>
public class PhotoStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Directory where photo files are written. Relative paths are resolved
    /// against the content root. Defaults to <c>photos</c>.
    /// </summary>
    public string PhotosPath { get; set; } = "photos";

    /// <summary>Maximum accepted upload size in bytes. Defaults to 5 MB.</summary>
    public long MaxPhotoBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum decoded image size in pixels (width × height). A small, highly-compressed
    /// file can declare enormous dimensions that would allocate gigabytes on decode
    /// (a decompression bomb); such uploads are rejected from the header before decoding.
    /// Defaults to 40 MP (comfortably above any phone camera).
    /// </summary>
    public long MaxImagePixels { get; set; } = 40_000_000;

    /// <summary>
    /// How long a signed photo URL stays valid, in minutes. Long enough to cover a
    /// browsing session's cached image references; short enough that a leaked URL
    /// stops working quickly. Defaults to 60.
    /// </summary>
    public int SignedUrlLifetimeMinutes { get; set; } = 60;
}
