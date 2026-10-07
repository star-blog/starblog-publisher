using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatContentImageServiceTests {
    [Theory]
    [InlineData("png", ".png")]
    [InlineData("jpg", ".jpg")]
    [InlineData("webp", ".jpg")]
    [InlineData("bmp", ".jpg")]
    [InlineData("gif", ".jpg")]
    public async Task PrepareAsync_DetectsRealFormatAndPreservesCompliantBytes(string format, string extension) {
        var path = Path.Combine(Path.GetTempPath(), $"wechat-test-{Guid.NewGuid():N}.img");
        try {
            using var image = new Image<Rgba32>(40, 20, new Rgba32(40, 80, 120));
            await SaveAsync(image, path, format);
            var original = await File.ReadAllBytesAsync(path);

            var result = await WeChatContentImageService.PrepareAsync(path);

            result.Extension.Should().Be(extension);
            Image.DetectFormat(result.Bytes).Name.Should().Be(extension == ".png" ? "PNG" : "JPEG");
            result.Bytes.Length.Should().BeLessThanOrEqualTo(WeChatContentImageService.MaxImageBytes);
            (await File.ReadAllBytesAsync(path)).Should().Equal(original);
            if (format is "png" or "jpg") result.Bytes.Should().Equal(original);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PrepareAsync_PreservesTransparencyWhenConvertingWebp() {
        var path = Path.Combine(Path.GetTempPath(), $"wechat-test-{Guid.NewGuid():N}.webp");
        try {
            using var image = new Image<Rgba32>(40, 20, new Rgba32(40, 80, 120, 80));
            await image.SaveAsWebpAsync(path);
            var result = await WeChatContentImageService.PrepareAsync(path);
            result.Extension.Should().Be(".png");
            using var decoded = Image.Load<Rgba32>(result.Bytes);
            decoded[0, 0].A.Should().Be(80);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(false, 1400, 1000)]
    [InlineData(true, 1800, 1200)]
    [InlineData(true, 8, 70000)]
    public async Task PrepareAsync_CompressesOversizedImagesIncludingNarrowImages(bool transparent, int width, int height) {
        var path = Path.Combine(Path.GetTempPath(), $"wechat-test-{Guid.NewGuid():N}.png");
        try {
            using var image = new Image<Rgba32>(width, height);
            var random = new Random(42);
            image.ProcessPixelRows(accessor => {
                for (var y = 0; y < accessor.Height; y++) {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++) {
                        row[x] = new Rgba32((byte)random.Next(256), (byte)random.Next(256),
                            (byte)random.Next(256), transparent ? (byte)random.Next(256) : (byte)255);
                    }
                }
            });
            await image.SaveAsPngAsync(path);
            var original = await File.ReadAllBytesAsync(path);
            original.Length.Should().BeGreaterThan(WeChatContentImageService.MaxImageBytes);

            var result = await WeChatContentImageService.PrepareAsync(path);

            result.Bytes.Length.Should().BeLessThanOrEqualTo(WeChatContentImageService.MaxImageBytes);
            result.Extension.Should().Be(transparent ? ".png" : ".jpg");
            using var decoded = Image.Load<Rgba32>(result.Bytes);
            decoded.Width.Should().BeLessThanOrEqualTo(width);
            decoded.Height.Should().BeLessThanOrEqualTo(height);
            if (transparent) decoded.Height.Should().BeLessThan(height);
            (await File.ReadAllBytesAsync(path)).Should().Equal(original);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PrepareAsync_RejectsInvalidImageBytes() {
        var path = Path.Combine(Path.GetTempPath(), $"wechat-test-{Guid.NewGuid():N}.jpg");
        try {
            await File.WriteAllTextAsync(path, "not an image");
            var action = () => WeChatContentImageService.PrepareAsync(path);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*无法识别正文图片格式*");
        }
        finally { File.Delete(path); }
    }

    private static Task SaveAsync(Image image, string path, string format) => format switch {
        "png" => image.SaveAsPngAsync(path),
        "jpg" => image.SaveAsJpegAsync(path),
        "webp" => image.SaveAsWebpAsync(path),
        "bmp" => image.SaveAsBmpAsync(path),
        "gif" => image.SaveAsGifAsync(path),
        _ => throw new ArgumentException(format)
    };
}
