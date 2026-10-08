using System.Net;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class CoverStudioViewModelTests {
    [Fact]
    public async Task SelectArticleBackground_ComposesLocalImageAndCancelsPendingRandomBackground() {
        var path = Path.Combine(Path.GetTempPath(), $"article-image-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, await SolidImageAsync(new Rgba32(0, 170, 0)));
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken randomToken = default;
        var factory = Factory((_, token) => { randomToken = token; return pending.Task; });
        PreparedWeChatCover? applied = null;
        using var studio = new CoverStudioViewModel(factory, "", cover => applied = cover,
            [new ArticleImageSource(path, "文章配图")]) { ShowScrim = false };
        try {
            studio.Start();
            await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[0]);
            randomToken.IsCancellationRequested.Should().BeTrue();
            studio.IsFetchingRandomBackground.Should().BeFalse();
            studio.IsFetchingArticleBackground.Should().BeFalse();
            studio.BackgroundDescription.Should().Be("文章图片 · 文章配图");
            using (var cover = Image.Load<Rgba32>(studio.OutputPath)) {
                cover.Width.Should().Be(1200);
                cover.Height.Should().Be(900);
                cover[600, 450].G.Should().BeGreaterThan(150);
                cover[600, 450].R.Should().BeLessThan(20);
            }

            studio.ApplyToWeChatCommand.Execute(null);
            applied.Should().NotBeNull();
            applied!.Value.Path.Should().Be(studio.OutputPath);
            studio.Dispose();
            File.Exists(applied.Value.Path).Should().BeTrue();
        }
        finally {
            pending.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });
            File.Delete(path);
            if (applied != null) File.Delete(applied.Value.Path);
        }
    }

    [Fact]
    public async Task SelectArticleBackground_DownloadsOnlineImageAndKeepsPreviousCoverOnFailure() {
        var bytes = await SolidImageAsync(new Rgba32(0, 0, 200));
        var requested = new List<Uri>();
        var factory = Factory((request, _) => {
            requested.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(request.RequestUri!.AbsolutePath == "/broken" ? [1, 2, 3] : bytes)
            });
        });
        using var studio = new CoverStudioViewModel(factory, "", _ => { }, [
            new ArticleImageSource("https://images.example/photo?id=1&size=2", "在线配图"),
            new ArticleImageSource("https://images.example/broken", "损坏的图片")
        ]) { ShowScrim = false };
        studio.Start();
        await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[0]);
        var previous = studio.OutputPath;
        using (var cover = Image.Load<Rgba32>(previous)) cover[600, 450].B.Should().BeGreaterThan(180);
        requested.Should().Contain(new Uri("https://images.example/photo?id=1&size=2"));

        await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[1]);

        studio.OutputPath.Should().Be(previous);
        studio.BackgroundDescription.Should().Be("文章图片 · 在线配图");
        studio.StatusMessage.Should().Contain("读取文章图片失败");
    }

    [Fact]
    public async Task SelectArticleBackground_ReportsMissingLocalImages() {
        using var studio = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "", _ => { },
            [new ArticleImageSource(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png"), "缺失图片")]);

        await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[0]);

        studio.OutputPath.Should().BeEmpty();
        studio.StatusMessage.Should().Contain("读取文章图片失败");
        studio.IsFetchingArticleBackground.Should().BeFalse();
    }

    private static IHttpClientFactory Factory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new Handler(send)));
        return factory.Object;
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private static async Task<byte[]> SolidImageAsync(Rgba32 color) {
        using var image = new Image<Rgba32>(48, 32, color);
        using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        return stream.ToArray();
    }
}
