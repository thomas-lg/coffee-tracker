using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CoffeeTracker.Tests;

// Exercises the REAL storage adapter against a throwaway temp directory: the size
// cap (declared and actual), the content-type allowlist + magic-number sniff, the
// decode/re-encode pipeline that strips polyglots, and delete/partial-write hygiene.
public sealed class FileSystemPhotoStorageTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ct-storage-").FullName;

    public void Dispose()
    {
        try
        {
            // Restore write permission in case a test removed it, then clean up.
            if (!OperatingSystem.IsWindows() && Directory.Exists(_dir))
            {
                File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort — leftover temp dirs are harmless.
        }
    }

    private FileSystemPhotoStorage NewStorage(long maxBytes = 5 * 1024 * 1024, long maxImagePixels = 40_000_000) =>
        new(
            Options.Create(new PhotoStorageOptions { PhotosPath = _dir, MaxPhotoBytes = maxBytes, MaxImagePixels = maxImagePixels }),
            NullLogger<FileSystemPhotoStorage>.Instance);

    private static byte[] RealImage(string contentType)
    {
        using var img = new Image<Rgba32>(2, 2);
        img[0, 0] = new Rgba32(255, 0, 0);
        img[1, 1] = new Rgba32(0, 0, 255);
        using var ms = new MemoryStream();
        switch (contentType)
        {
            case "image/jpeg": img.SaveAsJpeg(ms); break;
            case "image/png": img.SaveAsPng(ms); break;
            case "image/webp": img.SaveAsWebp(ms); break;
            default: throw new ArgumentOutOfRangeException(nameof(contentType));
        }
        return ms.ToArray();
    }

    private string[] StoredFiles() => Directory.Exists(_dir) ? Directory.GetFiles(_dir) : [];

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/webp", ".webp")]
    public async Task Save_accepts_a_real_image_and_the_stored_file_decodes(string contentType, string extension)
    {
        var storage = NewStorage();
        var bytes = RealImage(contentType);

        var result = await storage.SaveAsync(new MemoryStream(bytes), contentType, bytes.Length);

        Assert.Equal(PhotoStorageStatus.Stored, result.Status);
        Assert.StartsWith("photos/", result.RelativePath);
        Assert.EndsWith(extension, result.RelativePath);

        var stored = Assert.Single(StoredFiles());
        Assert.Equal(Path.GetFileName(result.RelativePath), Path.GetFileName(stored));
        // The re-encoded output must itself be a decodable image of the same size.
        using var reloaded = Image.Load(stored);
        Assert.Equal(2, reloaded.Width);
        Assert.Equal(2, reloaded.Height);
    }

    [Fact]
    public async Task Save_rejects_a_declared_length_over_the_cap()
    {
        var storage = NewStorage(maxBytes: 64);
        var bytes = RealImage("image/png");

        var result = await storage.SaveAsync(new MemoryStream(bytes), "image/png", length: 65);

        Assert.Equal(PhotoStorageStatus.TooLarge, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Save_rejects_a_stream_that_lies_about_its_length_but_overruns_the_cap()
    {
        // Declared length is tiny, but the actual stream exceeds the cap — the
        // adapter must measure what it buffered, not trust the declaration.
        var storage = NewStorage(maxBytes: 64);
        var bytes = RealImage("image/png"); // a real PNG is comfortably > 64 bytes

        Assert.True(bytes.Length > 64, "test premise: image larger than the cap");
        var result = await storage.SaveAsync(new MemoryStream(bytes), "image/png", length: 1);

        Assert.Equal(PhotoStorageStatus.TooLarge, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Save_rejects_a_disallowed_content_type()
    {
        var storage = NewStorage();
        var bytes = RealImage("image/png");

        var result = await storage.SaveAsync(new MemoryStream(bytes), "text/plain", bytes.Length);

        Assert.Equal(PhotoStorageStatus.InvalidContentType, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Save_rejects_bytes_that_dont_match_the_claimed_type()
    {
        var storage = NewStorage();
        var bytes = "hello, definitely not an image"u8.ToArray();

        var result = await storage.SaveAsync(new MemoryStream(bytes), "image/png", bytes.Length);

        Assert.Equal(PhotoStorageStatus.InvalidContentType, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Save_rejects_a_polyglot_that_sniffs_as_png_but_cannot_decode()
    {
        // Valid PNG magic number, garbage body: passes the header sniff but must be
        // caught by the decode step (the guard that strips embedded payloads).
        var storage = NewStorage();
        byte[] polyglot = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05];

        var result = await storage.SaveAsync(new MemoryStream(polyglot), "image/png", polyglot.Length);

        Assert.Equal(PhotoStorageStatus.InvalidContentType, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Save_rejects_an_image_whose_dimensions_exceed_the_pixel_cap()
    {
        // Decompression-bomb guard: the compressed bytes are tiny (a real 2×2 PNG) but
        // its 4 pixels exceed a 2-pixel cap, so it's rejected from the header before the
        // full decode would allocate. Proves the guard, not the byte cap.
        var storage = NewStorage(maxImagePixels: 2);
        var bytes = RealImage("image/png");

        var result = await storage.SaveAsync(new MemoryStream(bytes), "image/png", bytes.Length);

        Assert.Equal(PhotoStorageStatus.TooLarge, result.Status);
        Assert.Empty(StoredFiles());
    }

    [Fact]
    public async Task Delete_returns_true_only_when_a_file_was_actually_removed()
    {
        var storage = NewStorage();
        var bytes = RealImage("image/png");
        var stored = await storage.SaveAsync(new MemoryStream(bytes), "image/png", bytes.Length);

        Assert.True(await storage.DeleteAsync(stored.RelativePath!));
        Assert.Empty(StoredFiles());
        // Second delete: the file is already gone — idempotent, but reported false.
        Assert.False(await storage.DeleteAsync(stored.RelativePath!));
        Assert.False(await storage.DeleteAsync("photos/never-existed.png"));
    }

    [Fact]
    public async Task Failed_write_leaves_no_partial_file_behind()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // permission trick below is Unix-only; suite runs on macOS/Linux CI
        }

        var storage = NewStorage();
        var bytes = RealImage("image/png");

        // Make the photos directory unwritable so the file write throws mid-save.
        File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => storage.SaveAsync(new MemoryStream(bytes), "image/png", bytes.Length));
        }
        finally
        {
            File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Empty(StoredFiles()); // no partial/zero-byte file left behind
    }

    [Fact]
    public async Task List_returns_stored_paths_and_empty_when_nothing_stored()
    {
        var storage = NewStorage();
        Assert.Empty(await storage.ListAsync());

        var bytes = RealImage("image/jpeg");
        var stored = await storage.SaveAsync(new MemoryStream(bytes), "image/jpeg", bytes.Length);

        var listed = Assert.Single(await storage.ListAsync());
        Assert.Equal(stored.RelativePath, listed);
    }
}
