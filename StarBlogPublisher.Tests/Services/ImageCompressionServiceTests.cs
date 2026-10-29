using FluentAssertions;
using StarBlogPublisher.Services;
using StarBlogPublisher.Tests.Services.Imaging;

namespace StarBlogPublisher.Tests.Services;

public class ImageCompressionServiceTests {
    [Fact]
    public async Task CompressImageAsync_KeepsAnimatedGifFrames() {
        var path = Path.Combine(Path.GetTempPath(), $"starblog-gif-{Guid.NewGuid():N}.gif");
        var original = SkiaFixture.EncodeTwoFrameGif();
        SkiaFixture.FrameCount(original).Should().Be(2);
        await File.WriteAllBytesAsync(path, original);
        using var service = new ImageCompressionService();
        try {
            var result = await service.CompressImageAsync(path);

            result.Success.Should().BeTrue();
            result.CompressionRatio.Should().Be(0);
            var output = await File.ReadAllBytesAsync(result.CompressedPath);
            output.Should().Equal(original);
            SkiaFixture.FrameCount(output).Should().Be(2);
        }
        finally {
            File.Delete(path);
        }
    }
}
