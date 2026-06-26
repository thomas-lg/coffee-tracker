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
}
