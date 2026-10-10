using CoffeeTracker.Infrastructure.Imaging;
using SkiaSharp;

namespace CoffeeTracker.Infrastructure.Ocr;

/// <summary>
/// Pipes an image to an OCR engine, turning it the right way up first when the camera
/// said to.
///
/// A phone does not rotate pixels when you turn it: it writes them in sensor order and
/// records an EXIF orientation tag. Every viewer honours that tag, so the photo looks
/// upright everywhere the user has seen it. The libraries the engines decode with do not:
/// Leptonica under Tesseract ignores it, and so does OpenCV under RapidOCR. Without this,
/// a bag photographed in portrait reaches the engine on its side and comes back as
/// mirrored nonsense.
///
/// Shared rather than duplicated because it is a property of cameras, not of any one
/// engine, and an engine added later would hit it too.
/// </summary>
internal static class UprightImage
{
    public static async Task WriteToAsync(Stream image, Stream destination, CancellationToken ct)
    {
        using var buffered = new MemoryStream();
        await image.CopyToAsync(buffered, ct);
        var bytes = buffered.GetBuffer().AsMemory(0, (int)buffered.Length);

        using var upright = Decode(bytes);
        if (upright is null)
        {
            // Upright, which is the overwhelmingly common case, or undecodable, which is
            // not this method's call to make: the application layer already validated the
            // bytes, so the engine gets them as they came and refuses them if it disagrees.
            await destination.WriteAsync(bytes, ct);
            return;
        }

        // PNG, not JPEG: re-encoding would lay a second generation of block artefacts
        // over the camera's own, right on the glyph edges the engine reads.
        using var png = upright.Encode(SKEncodedImageFormat.Png, 100);
        await png.AsStream().CopyToAsync(destination, ct);
    }

    /// <summary>
    /// Only pays for a decode when the camera tagged the photo as rotated; the codec reads
    /// the orientation from the header alone.
    /// </summary>
    private static SKBitmap? Decode(ReadOnlyMemory<byte> bytes)
    {
        using var data = SKData.CreateCopy(bytes.Span);
        using var codec = SKCodec.Create(data);
        return codec is null || codec.EncodedOrigin == SKEncodedOrigin.TopLeft
            ? null
            : OrientedImage.Decode(codec);
    }
}
