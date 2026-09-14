using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Infrastructure.Ocr;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;
using Xunit;
using Xunit.Abstractions;

namespace CoffeeTracker.Tests.Ocr;

/// <summary>
/// The one snap-to-fill failure the benchmark corpus cannot show, because rendered
/// fixtures carry no EXIF and every one of them is already upright.
///
/// A phone does not rotate the pixels when you turn it: it stores them in sensor order
/// and writes an EXIF orientation tag saying which way is up. Every viewer honours that
/// tag, so the photo looks right everywhere the user has seen it. Leptonica, which is
/// what Tesseract decodes JPEG with, does not. The engine is handed the sideways
/// pixels, which is the single most likely reason a scan of a perfectly readable bag
/// comes back with nothing.
/// </summary>
public sealed class TesseractOrientationTests(ITestOutputHelper output)
{
    private TesseractCliOcrService NewService() => new(
        Options.Create(new OcrOptions { TimeoutSeconds = 120, MaxConcurrency = 1 }),
        new TestOutputLogger<TesseractCliOcrService>(output));

    /// <summary>
    /// Takes an upright fixture and stores it the way a phone held sideways would: the
    /// pixels turned, and an EXIF tag saying to turn them back. Nothing about how the
    /// image *looks* changes, only how it is encoded.
    /// </summary>
    private static async Task<MemoryStream> AsPhoneWouldStoreIt(string fixture)
    {
        using var picture = await Image.LoadAsync(
            Path.Combine(OcrFixtures.Root, "synthetic", fixture));

        // Orientation 6 means "the top of the subject is on the right", so a viewer
        // rotates 90° clockwise to show it. Counter-rotating the pixels here is what
        // makes that tag true rather than decorative.
        picture.Mutate(x => x.Rotate(RotateMode.Rotate270));
        (picture.Metadata.ExifProfile ??= new ExifProfile())
            .SetValue(ExifTag.Orientation, (ushort)6);

        var stored = new MemoryStream();
        await picture.SaveAsync(stored, new JpegEncoder { Quality = 90 });
        stored.Position = 0;
        return stored;
    }

    [OcrBenchmarkFact(OcrEngine.Tesseract)]
    public async Task A_photo_the_camera_tagged_as_rotated_is_read_upright()
    {
        using var photo = await AsPhoneWouldStoreIt("kirinyaga-flat.jpg");

        var read = await NewService().ReadAsync(photo);

        Assert.True(read.Available);
        // The bag's own name. Before the adapter applied the tag, this came back from a
        // page of sideways glyphs with none of the label's words in it.
        Assert.Contains("Kirinyaga", read.RawText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The common case has to stay untouched: most uploads carry no EXIF at all, and
    /// paying a full decode and re-encode on every one of them to honour a tag that is
    /// not there would be a cost with nothing bought.
    /// </summary>
    [OcrBenchmarkFact(OcrEngine.Tesseract)]
    public async Task An_image_with_no_orientation_tag_still_reads()
    {
        await using var upright = File.OpenRead(
            Path.Combine(OcrFixtures.Root, "synthetic", "kirinyaga-flat.jpg"));

        var read = await NewService().ReadAsync(upright);

        Assert.True(read.Available);
        Assert.Contains("Kirinyaga", read.RawText, StringComparison.OrdinalIgnoreCase);
    }
}
