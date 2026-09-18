using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatCoverImageServiceTests {
    [Fact]
    public async Task PrepareLocalAsync_CropsImageToRequestedWeChatCoverSize() {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"starblog-cover-source-{Guid.NewGuid():N}.png");
        string? outputPath = null;
        try {
            using (var source = new Image<Rgba32>(1400, 700)) {
                await source.SaveAsPngAsync(sourcePath);
            }

            var service = new WeChatCoverImageService(new Mock<IHttpClientFactory>().Object);
            outputPath = await service.PrepareLocalAsync(sourcePath, 900, 383);

            Assert.EndsWith(".jpg", outputPath, StringComparison.OrdinalIgnoreCase);
            using var result = await Image.LoadAsync(outputPath);
            Assert.Equal(900, result.Width);
            Assert.Equal(383, result.Height);
        }
        finally {
            File.Delete(sourcePath);
            if (outputPath != null) File.Delete(outputPath);
        }
    }
}
