using CoffeeTracker.Infrastructure.Ocr;
using SkiaSharp;
using Xunit;

namespace CoffeeTracker.Tests.Ocr;

/// <summary>
/// TesseractOrientationTests proves the outcome but only runs where Tesseract is
/// installed; these pin the rotation itself everywhere.
/// </summary>
public sealed class UprightImageTests
{
    private static async Task<byte[]> Run(byte[] input)
    {
        using var output = new MemoryStream();
        await UprightImage.WriteToAsync(new MemoryStream(input), output, CancellationToken.None);
        return output.ToArray();
    }

    [Fact]
    public async Task An_untagged_photo_reaches_the_engine_byte_for_byte()
    {
        using var picture = TestImages.HalfRedHalfBlue(16, 8);
        var jpeg = TestImages.Encode(picture, SKEncodedImageFormat.Jpeg);

        Assert.Equal(jpeg, await Run(jpeg));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public async Task A_photo_tagged_as_rotated_reaches_the_engine_upright(ushort orientation)
    {
        using var sensor = TestImages.HalfRedHalfBlue(16, 8);
        var tagged = TestImages.WithExifOrientation(TestImages.Encode(sensor, SKEncodedImageFormat.Jpeg), orientation);

        using var upright = SKBitmap.Decode(await Run(tagged));

        Assert.Equal(8, upright.Width);
        Assert.Equal(16, upright.Height);
        // 6 turns the sensor's left half to the top, 8 to the bottom.
        var (top, bottom) = orientation == 6 ? (SKColors.Red, SKColors.Blue) : (SKColors.Blue, SKColors.Red);
        AssertNear(top, upright.GetPixel(4, 2));
        AssertNear(bottom, upright.GetPixel(4, 13));
    }

    [Fact]
    public async Task Bytes_that_are_not_an_image_are_left_for_the_engine_to_refuse()
    {
        var garbage = "not an image at all"u8.ToArray();

        Assert.Equal(garbage, await Run(garbage));
    }

    private static void AssertNear(SKColor expected, SKColor actual) =>
        Assert.True(
            Math.Abs(expected.Red - actual.Red) < 40
                && Math.Abs(expected.Green - actual.Green) < 40
                && Math.Abs(expected.Blue - actual.Blue) < 40,
            $"expected {expected}, got {actual}");
}
