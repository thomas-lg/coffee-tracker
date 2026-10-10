using SkiaSharp;

namespace CoffeeTracker.Tests;

internal static class TestImages
{
    /// <summary>Left half red, right half blue, so an orientation can be read back.</summary>
    public static SKBitmap HalfRedHalfBlue(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);
        using var red = new SKPaint { Color = SKColors.Red };
        canvas.DrawRect(0, 0, width / 2f, height, red);
        return bitmap;
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality = 90)
    {
        using var data = bitmap.Encode(format, quality);
        return data.ToArray();
    }

    /// <summary>Turns the pixels a quarter turn counter-clockwise.</summary>
    public static SKBitmap Rotate270(SKBitmap source)
    {
        var rotated = new SKBitmap(source.Info.WithSize(source.Height, source.Width));
        using var canvas = new SKCanvas(rotated);
        canvas.SetMatrix(new SKMatrix(0, 1, 0, -1, 0, source.Width, 0, 0, 1));
        using var pixels = SKImage.FromBitmap(source);
        canvas.DrawImage(pixels, 0, 0, SKSamplingOptions.Default);
        return rotated;
    }

    /// <summary>
    /// Skia writes no EXIF, so the tag a phone would record is spliced in by hand: an APP1
    /// segment holding a one-entry big-endian TIFF directory, right after the JPEG's
    /// start-of-image marker.
    /// </summary>
    public static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] payload =
        [
            .. "Exif\0\0"u8,
            .. "MM"u8, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        var length = payload.Length + 2;
        return [.. jpeg[..2], 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, .. jpeg[2..]];
    }
}
