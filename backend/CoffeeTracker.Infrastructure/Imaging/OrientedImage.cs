using SkiaSharp;

namespace CoffeeTracker.Infrastructure.Imaging;

/// <summary>
/// Decodes an image with its EXIF orientation applied to the pixels. Skia's decoder
/// reports the tag but leaves the pixels in sensor order, and its encoders write no
/// EXIF, so a re-encoded portrait photo would otherwise come out on its side.
/// </summary>
internal static class OrientedImage
{
    public static SKBitmap? Decode(SKCodec codec)
    {
        var decoded = SKBitmap.Decode(codec);
        var origin = codec.EncodedOrigin;
        if (decoded is null || origin == SKEncodedOrigin.TopLeft)
        {
            return decoded;
        }

        using (decoded)
        {
            var swapsAxes = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var width = swapsAxes ? decoded.Height : decoded.Width;
            var height = swapsAxes ? decoded.Width : decoded.Height;

            var upright = new SKBitmap(decoded.Info.WithSize(width, height));
            using var canvas = new SKCanvas(upright);
            canvas.SetMatrix(ToUpright(origin, width, height));
            using var pixels = SKImage.FromBitmap(decoded);
            canvas.DrawImage(pixels, 0, 0, SKSamplingOptions.Default);
            return upright;
        }
    }

    /// <summary>
    /// Skia's own SkEncodedOriginToMatrix, which SkiaSharp does not expose. Width and
    /// height are the upright image's.
    /// </summary>
    private static SKMatrix ToUpright(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, width, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, width, -1, 0, height, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, height, 0, 0, 1),
        _ => SKMatrix.Identity,
    };
}
