using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Storage;

/// <summary>
/// Driven adapter: stores uploaded photos on the local filesystem. Owns the
/// storage-security decisions — content-type allowlist, size cap, and
/// server-generated filenames — so no client-supplied path ever reaches disk.
/// </summary>
public class FileSystemPhotoStorage : IPhotoStorage
{
    /// <summary>
    /// Allowed upload content types mapped to the extension we store them under.
    /// The extension is derived from this map, never from the client filename.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };

    /// <summary>Request-relative path segment the photos directory is served under.</summary>
    private const string PublicPrefix = "photos";

    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly ILogger<FileSystemPhotoStorage> _logger;

    public FileSystemPhotoStorage(IOptions<PhotoStorageOptions> options, ILogger<FileSystemPhotoStorage> logger)
    {
        _directory = Path.GetFullPath(options.Value.PhotosPath);
        _maxBytes = options.Value.MaxPhotoBytes;
        _logger = logger;
    }

    public async Task<PhotoStorageResult> SaveAsync(Stream content, string? contentType, long length, CancellationToken ct = default)
    {
        if (contentType is null || !AllowedTypes.TryGetValue(contentType, out var extension))
        {
            return PhotoStorageResult.Rejected(PhotoStorageStatus.InvalidContentType);
        }

        if (length > _maxBytes)
        {
            return PhotoStorageResult.Rejected(PhotoStorageStatus.TooLarge);
        }

        Directory.CreateDirectory(_directory);

        // Server-generated name: no client input contributes to the path, so
        // directory traversal is structurally impossible.
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_directory, fileName);

        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(file, ct);

            // Guard against a stream that lied about its length (chunked uploads
            // where Length was 0/unknown): if it overran the cap, discard it.
            if (file.Length > _maxBytes)
            {
                file.Close();
                File.Delete(fullPath);
                return PhotoStorageResult.Rejected(PhotoStorageStatus.TooLarge);
            }
        }

        return PhotoStorageResult.Stored($"{PublicPrefix}/{fileName}");
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        // Resolve by filename only, so a stored value can never point outside the
        // photos directory (defence-in-depth even though we generate the names).
        var fileName = Path.GetFileName(relativePath);
        if (!string.IsNullOrEmpty(fileName))
        {
            var fullPath = Path.Combine(_directory, fileName);
            try
            {
                // File.Delete is a no-op if the file is already gone (idempotent),
                // but it still throws for a missing directory, a locked file, or a
                // permission problem. Cleanup is best-effort: a failure here must not
                // turn an already-committed delete/replace into a 500, so swallow and
                // log — an orphaned file is recoverable, a failed user operation isn't.
                File.Delete(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to delete stored photo {RelativePath}; leaving it as an orphan.", relativePath);
            }
        }

        return Task.CompletedTask;
    }
}
