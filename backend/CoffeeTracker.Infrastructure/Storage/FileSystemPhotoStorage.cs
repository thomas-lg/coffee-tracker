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

        // The Content-Type header is client-controlled, so acceptance (and the stored
        // extension) must not rest on it alone: read the leading bytes and confirm the
        // file's actual magic-number signature matches the claimed type. Reading the
        // header here also lets us reconstruct the full content below without requiring
        // a seekable stream.
        var header = new byte[HeaderBytes];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        if (!SignatureMatches(contentType, header.AsSpan(0, headerLength)))
        {
            return PhotoStorageResult.Rejected(PhotoStorageStatus.InvalidContentType);
        }

        Directory.CreateDirectory(_directory);

        // Server-generated name: no client input contributes to the path, so
        // directory traversal is structurally impossible.
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_directory, fileName);

        await using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            // Write the header bytes already consumed for sniffing, then the remainder.
            await file.WriteAsync(header.AsMemory(0, headerLength), ct);
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

    /// <summary>Bytes to read for signature sniffing (WebP needs the first 12).</summary>
    private const int HeaderBytes = 12;

    /// <summary>The 8-byte PNG file signature (89 50 4E 47 0D 0A 1A 0A).</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Confirms the file-signature (magic number) of <paramref name="header"/> matches
    /// the claimed <paramref name="contentType"/> — JPEG (FF D8 FF), PNG (89 50 4E 47
    /// 0D 0A 1A 0A), or WebP (RIFF....WEBP).
    /// </summary>
    private static bool SignatureMatches(string contentType, ReadOnlySpan<byte> header)
    {
        if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        }

        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            return header.Length >= 8 && header[..8].SequenceEqual(PngSignature);
        }

        if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return header.Length >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WEBP"u8);
        }

        return false;
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

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        // Storage is a flat directory of server-named files; return each as the same
        // `photos/{name}` relative path SaveAsync hands out. No directory yet ⇒ nothing
        // stored ⇒ empty list (not an error).
        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> paths = Directory.EnumerateFiles(_directory)
            .Select(f => $"{PublicPrefix}/{Path.GetFileName(f)}")
            .ToList();
        return Task.FromResult(paths);
    }
}
