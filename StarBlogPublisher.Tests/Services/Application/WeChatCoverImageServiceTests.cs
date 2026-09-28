using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatCoverImageServiceTests {
    [Fact]
    public async Task PrepareLocalAsync_KeepsTheSourceAspectRatio() {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"starblog-cover-source-{Guid.NewGuid():N}.png");
        string? outputPath = null;
        try {
            using (var source = new Image<Rgba32>(1400, 700)) {
                await source.SaveAsPngAsync(sourcePath);
            }

            var service = new WeChatCoverImageService(new Mock<IHttpClientFactory>().Object);
            var prepared = await service.PrepareLocalAsync(sourcePath);
            outputPath = prepared.Path;

            Assert.EndsWith(".jpg", prepared.Path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1400, prepared.Width);
            Assert.Equal(700, prepared.Height);
            using var result = await Image.LoadAsync(prepared.Path);
            Assert.Equal(1400, result.Width);
            Assert.Equal(700, result.Height);
        }
        finally {
            File.Delete(sourcePath);
            if (outputPath != null) File.Delete(outputPath);
        }
    }
}

public class WeChatCoverCropsTests {
    [Fact]
    public void Center_UsesBothWeChatRatios() {
        var crops = WeChatCoverCrops.Center(1400, 700);

        crops.Should().HaveCount(2);
        crops[0].Ratio.Should().Be("2.35_1");
        crops[0].X1.Should().Be("0.0000");
        crops[0].X2.Should().Be("1.0000");
        crops[0].Y1.Should().Be("0.0745");
        crops[0].Y2.Should().Be("0.9255");
        crops[1].Ratio.Should().Be("1_1");
        crops[1].X1.Should().Be("0.2500");
        crops[1].Y1.Should().Be("0.0000");
        crops[1].X2.Should().Be("0.7500");
        crops[1].Y2.Should().Be("1.0000");
    }
}
