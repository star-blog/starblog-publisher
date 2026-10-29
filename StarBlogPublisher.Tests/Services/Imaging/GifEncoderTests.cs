using FluentAssertions;
using SkiaSharp;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Tests.Services.Imaging;

public class GifEncoderTests {
    [Fact]
    public void Encode_RoundTripsPaletteAndTransparency() {
        const int width = 128;
        const int height = 128;
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var colors = new byte[32 * 3];
        for (var i = 0; i < 32; i++) {
            colors[i * 3] = (byte)(i * 8);
            colors[i * 3 + 1] = (byte)(255 - i * 8);
            colors[i * 3 + 2] = (byte)((i * 40) & 255);
        }

        var random = new Random(7);
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var index = y * bitmap.RowBytes + x * 4;
                if (x == 3 && y == 5) {
                    pixels[index + 3] = 0;
                    continue;
                }

                var color = random.Next(32) * 3;
                pixels[index] = colors[color];
                pixels[index + 1] = colors[color + 1];
                pixels[index + 2] = colors[color + 2];
                pixels[index + 3] = 255;
            }
        }

        using var decoded = DecodedImage.Decode(GifEncoder.Encode(bitmap));
        decoded.Format.Should().Be(SKEncodedImageFormat.Gif);
        decoded.Width.Should().Be(width);
        decoded.Height.Should().Be(height);
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var index = y * bitmap.RowBytes + x * 4;
                var actual = decoded.Pixel(x, y);
                if (x == 3 && y == 5) {
                    actual.A.Should().Be(0);
                    continue;
                }

                actual.R.Should().Be(pixels[index]);
                actual.G.Should().Be(pixels[index + 1]);
                actual.B.Should().Be(pixels[index + 2]);
                actual.A.Should().Be(255);
            }
        }
    }
}
