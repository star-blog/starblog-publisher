using FluentAssertions;
using SkiaSharp;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Tests.Services.Application;

public class SkiaOrientationTests {
    [Fact]
    public void ApplyOrigin_RotatesNinetyDegreesClockwise() {
        using var oriented = Orient(SKEncodedOrigin.RightTop);
        Pixel(oriented, 0, 0).Should().Be(30);
        Pixel(oriented, 1, 0).Should().Be(10);
        Pixel(oriented, 0, 1).Should().Be(255);
        Pixel(oriented, 1, 1).Should().Be(20);
    }

    [Fact]
    public void ApplyOrigin_RotatesNinetyDegreesCounterClockwise() {
        using var oriented = Orient(SKEncodedOrigin.LeftBottom);
        Pixel(oriented, 0, 0).Should().Be(20);
        Pixel(oriented, 1, 0).Should().Be(255);
        Pixel(oriented, 0, 1).Should().Be(10);
        Pixel(oriented, 1, 1).Should().Be(30);
    }

    [Fact]
    public void ApplyOrigin_FlipsHorizontally() {
        using var oriented = Orient(SKEncodedOrigin.TopRight);
        Pixel(oriented, 0, 0).Should().Be(20);
        Pixel(oriented, 1, 0).Should().Be(10);
    }

    private static SKBitmap Orient(SKEncodedOrigin origin) {
        var source = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Opaque));
        Set(source, 0, 0, 10, 0, 0);
        Set(source, 1, 0, 0, 20, 0);
        Set(source, 0, 1, 0, 0, 30);
        Set(source, 1, 1, 255, 255, 255);
        return DecodedImage.ApplyOrigin(source, origin);
    }

    private static void Set(SKBitmap bitmap, int x, int y, byte r, byte g, byte b) {
        var pixels = bitmap.GetPixelSpan();
        var index = y * bitmap.RowBytes + x * 4;
        pixels[index] = r;
        pixels[index + 1] = g;
        pixels[index + 2] = b;
        pixels[index + 3] = 255;
    }

    private static byte Pixel(SKBitmap bitmap, int x, int y) {
        var pixels = bitmap.GetPixelSpan();
        var index = y * bitmap.RowBytes + x * 4;
        return (byte)Math.Max(pixels[index], Math.Max(pixels[index + 1], pixels[index + 2]));
    }
}
