using FluentAssertions;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Tests.Services.Imaging;

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
            await SkiaFixture.SaveAsync(path, 40, 20, 40, 80, 120, 255, format);
            var original = await File.ReadAllBytesAsync(path);

            var result = await WeChatContentImageService.PrepareAsync(path);

            result.Extension.Should().Be(extension);
            SkiaFixture.Detect(result.Bytes).Should().Be(extension == ".png" ? "PNG" : "JPEG");
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
            await SkiaFixture.SaveAsync(path, 40, 20, 40, 80, 120, 80, "webp");
            var result = await WeChatContentImageService.PrepareAsync(path);
            result.Extension.Should().Be(".png");
            SkiaFixture.Alpha(result.Bytes, 0, 0).Should().Be(80);
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
            await SkiaFixture.SaveRandomAsync(path, width, height, transparent, 42, "png");
            var original = await File.ReadAllBytesAsync(path);
            original.Length.Should().BeGreaterThan(WeChatContentImageService.MaxImageBytes);

            var result = await WeChatContentImageService.PrepareAsync(path);

            result.Bytes.Length.Should().BeLessThanOrEqualTo(WeChatContentImageService.MaxImageBytes);
            result.Extension.Should().Be(transparent ? ".png" : ".jpg");
            var decoded = SkiaFixture.Size(result.Bytes);
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
}
