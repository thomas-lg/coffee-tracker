namespace CoffeeTracker.Infrastructure.Storage;

/// <summary>
/// The URL segment under which stored photos are served.
///
/// Four places have to agree on this or photos break in ways that don't look related:
/// the storage adapter builds relative paths with it, the URL signer signs and emits
/// them, and the API both intercepts the segment to check signatures and mounts the
/// static-file provider at it. Signing one prefix and serving another yields a 401 on
/// every image with no clue as to why, so the value lives in one place.
///
/// Not the same thing as <see cref="PhotoStorageOptions.PhotosPath"/>, which is the
/// directory on disk and is configurable per deployment.
/// </summary>
public static class PhotoRoute
{
    /// <summary>Without slashes: <c>photos</c>.</summary>
    public const string PublicPrefix = "photos";

    /// <summary>Leading slash, for route matching and static-file mounting: <c>/photos</c>.</summary>
    public const string RequestPath = "/" + PublicPrefix;
}
