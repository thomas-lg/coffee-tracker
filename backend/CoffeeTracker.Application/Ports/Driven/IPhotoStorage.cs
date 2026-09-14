namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Driven (output) port for persisting uploaded coffee-bag photos. The adapter
/// owns the storage-security decisions (content-type allowlist, size cap,
/// server-generated filenames) so the application layer never touches the
/// filesystem or trusts client-supplied paths.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>
    /// Validates and stores an uploaded image. On success the result carries a
    /// path relative to the photos directory; otherwise it carries the reason
    /// the upload was rejected and nothing is written to disk.
    /// </summary>
    Task<PhotoStorageResult> SaveAsync(Stream content, string? contentType, long length, CancellationToken ct = default);

    /// <summary>
    /// Runs every check <see cref="SaveAsync"/> would run, content type, size, magic
    /// number, decompression-bomb ceiling, and whether the bytes actually decode, and
    /// writes nothing.
    ///
    /// For callers that need to know an upload is a real image without keeping it: the
    /// scan endpoint hands the bytes to an OCR process and has no use for them
    /// afterwards. Storing to validate and then deleting would be the same checks plus a
    /// file that exists only long enough to be orphaned.
    ///
    /// Shares <see cref="PhotoStorageStatus"/> with <see cref="SaveAsync"/> so a rejection
    /// reason means the same thing on both paths. Read <c>Stored</c> as "accepted" here:
    /// the checks passed and nothing was written.
    /// </summary>
    Task<PhotoStorageStatus> ValidateAsync(Stream content, string? contentType, long length, CancellationToken ct = default);

    /// <summary>
    /// Deletes a previously stored photo identified by the relative path returned
    /// from <see cref="SaveAsync"/>. Best-effort and idempotent: a missing file is
    /// not an error. Returns true only if a file actually existed and was removed, so
    /// callers (e.g. admin cleanup) can report accurate counts. Used to avoid orphaning
    /// files when a photo is replaced or its coffee deleted.
    /// </summary>
    Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Returns the relative paths of every stored photo, in the same shape
    /// <see cref="SaveAsync"/> returns, for administrative auditing. Returns an empty
    /// list when nothing has been stored yet.
    /// </summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default);
}

/// <summary>Outcome of a <see cref="IPhotoStorage.SaveAsync"/> call.</summary>
public enum PhotoStorageStatus
{
    Stored,
    InvalidContentType,
    TooLarge,
}

/// <summary>
/// Result of attempting to store a photo. <see cref="RelativePath"/> is non-null
/// only when <see cref="Status"/> is <see cref="PhotoStorageStatus.Stored"/>.
/// </summary>
public sealed record PhotoStorageResult(PhotoStorageStatus Status, string? RelativePath)
{
    public static PhotoStorageResult Stored(string relativePath) => new(PhotoStorageStatus.Stored, relativePath);
    public static PhotoStorageResult Rejected(PhotoStorageStatus status) => new(status, null);
}
