using System.Text;
using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Ports.Driving;
using CoffeeTracker.Application.Services;
using Xunit;

namespace CoffeeTracker.Tests;

// Scan service against fakes, verifies the orchestration (OCR availability gate,
// upload validation, photo retention, parse) without native libs. Uses the real
// CoffeeLabelParser since it's pure.
public class CoffeeScanServiceTests
{
    private sealed class FakeOcr(bool available, string text = "") : IOcrService
    {
        public bool IsAvailable { get; } = available;
        public bool AvailableButFailsRead { get; init; }

        public Task<OcrResult> ReadAsync(Stream image, CancellationToken ct = default) =>
            Task.FromResult(AvailableButFailsRead ? OcrResult.Unavailable : OcrResult.Read(text));
    }

    private sealed class FakePhotoStorage(PhotoStorageResult result) : IPhotoStorage
    {
        public int SaveCalls { get; private set; }
        public int ValidateCalls { get; private set; }
        public List<string> Deleted { get; } = [];

        public Task<PhotoStorageResult> SaveAsync(Stream content, string? contentType, long length, CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.FromResult(result);
        }

        public Task<PhotoStorageStatus> ValidateAsync(Stream content, string? contentType, long length, CancellationToken ct = default)
        {
            ValidateCalls++;
            return Task.FromResult(result.Status);
        }

        public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            Deleted.Add(relativePath);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static Stream Image() => new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));

    private static CoffeeScanService NewService(IOcrService ocr, FakePhotoStorage storage) =>
        new(ocr, storage, new CoffeeLabelParser());

    [Fact]
    public async Task ScanAsync_ReturnsOcrUnavailable_AndStoresNothing_WhenOcrDisabled()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/x.jpg"));
        var service = NewService(new FakeOcr(available: false), storage);

        var result = await service.ScanAsync(Image(), "image/jpeg", 16);

        Assert.Equal(ScanStatus.OcrUnavailable, result.Status);
        Assert.Equal(0, storage.ValidateCalls); // short-circuits before doing any work
    }

    [Fact]
    public async Task ScanAsync_ReturnsInvalidContentType_WhenStorageRejects()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Rejected(PhotoStorageStatus.InvalidContentType));
        var service = NewService(new FakeOcr(available: true, text: "irrelevant"), storage);

        var result = await service.ScanAsync(Image(), "text/plain", 16);

        Assert.Equal(ScanStatus.InvalidContentType, result.Status);
    }

    [Fact]
    public async Task ScanAsync_ReturnsParsedFields_OnSuccess()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/bag.jpg"));
        var ocrText = "Stumptown Coffee Roasters\nMedium · Ethiopia\n340g";
        var service = NewService(new FakeOcr(available: true, text: ocrText), storage);

        var result = await service.ScanAsync(Image(), "image/jpeg", 16);

        Assert.Equal(ScanStatus.Success, result.Status);
        Assert.Equal(ocrText, result.Response!.RawText);
        Assert.Equal("Ethiopia", result.Response.Parsed.Origin);
        Assert.Equal("Medium", result.Response.Parsed.RoastLevel);
        Assert.Equal("340g", result.Response.Parsed.Weight);
    }

    // The point of the change: a scan reads the bag and keeps nothing. Storing the photo
    // here left an orphan on every scan, because the client uploads the image again when
    // it saves the coffee, which left the admin cleanup as the only thing bounding the
    // photos directory.
    [Fact]
    public async Task ScanAsync_WritesNoFile_OnAnyPath()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Stored("photos/bag.jpg"));

        foreach (var ocr in new IOcrService[]
        {
            new FakeOcr(available: true, text: "Ethiopia"),
            new FakeOcr(available: true) { AvailableButFailsRead = true },
        })
        {
            await NewService(ocr, storage).ScanAsync(Image(), "image/jpeg", 16);
        }

        Assert.Equal(0, storage.SaveCalls);
        Assert.Empty(storage.Deleted); // nothing written, so nothing to compensate for
    }

    // Validation is not optional just because nothing is kept: the OCR process must not
    // be handed a decompression bomb or a file that only claims to be an image.
    [Fact]
    public async Task ScanAsync_ValidatesBeforeReachingTheEngine()
    {
        var storage = new FakePhotoStorage(PhotoStorageResult.Rejected(PhotoStorageStatus.TooLarge));
        var service = NewService(new FakeOcr(available: true, text: "never read"), storage);

        var result = await service.ScanAsync(Image(), "image/jpeg", 16);

        Assert.Equal(ScanStatus.TooLarge, result.Status);
        Assert.Equal(1, storage.ValidateCalls);
    }
}
