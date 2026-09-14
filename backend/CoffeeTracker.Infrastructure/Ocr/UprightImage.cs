using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

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
        // The header has to be read and then the same bytes replayed. The one caller
        // hands over a MemoryStream, but the port promises only a Stream.
        Stream source = image;
        MemoryStream? buffered = null;
        if (!image.CanSeek)
        {
            buffered = new MemoryStream();
            await image.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            source = buffered;
        }

        try
        {
            var start = source.Position;
            if (!await IsRotatedAsync(source, ct))
            {
                // The overwhelmingly common case, and it stays free: no decode, no
                // re-encode, the upload's own bytes straight down the pipe.
                source.Position = start;
                await source.CopyToAsync(destination, ct);
                return;
            }

            source.Position = start;
            using var picture = await Image.LoadAsync(source, ct);
            picture.Mutate(x => x.AutoOrient());
            // PNG, not JPEG: re-encoding would lay a second generation of block artefacts
            // over the camera's own, right on the glyph edges the engine reads.
            await picture.SaveAsync(destination, new PngEncoder(), ct);
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    /// <summary>Reads only the header, so an upright photo is never decoded twice.</summary>
    private static async Task<bool> IsRotatedAsync(Stream source, CancellationToken ct)
    {
        try
        {
            var info = await Image.IdentifyAsync(source, ct);
            return info.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var orientation) == true
                && orientation?.Value is > 1;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // Undecodable here is not this method's call to make: the application layer
            // already validated the bytes, so hand them over and let the engine refuse
            // them if it disagrees.
            return false;
        }
    }
}
