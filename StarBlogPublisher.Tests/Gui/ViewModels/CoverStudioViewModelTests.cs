using System.Net;
using FluentAssertions;
using Moq;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Tests.Services.Imaging;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class CoverStudioViewModelTests {
    static CoverStudioViewModelTests() => Avalonia.Skia.SkiaPlatform.Initialize();
    [Fact]
    public async Task PointerInteraction_ReusesLayersAndCommitsOnlyOnce_AndEscapeDiscards() {
        using var studio = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "移动标题", _ => { });
        await studio.RenderNowAsync();
        studio.HasPreview.Should().BeTrue(studio.StatusMessage);
        var layer = studio.Preview;
        var revision = studio.Session.Revision;
        var changes = 0;
        studio.Session.Changed += () => changes++;
        studio.BeginInteraction().Should().BeTrue();
        for (var i = 0; i < 1000; i++) studio.UpdateInteraction(studio.LiveDocument with { TextX = i % 80, TextY = i % 60 });
        studio.Session.State.Should().BeNull();
        studio.Session.Revision.Should().Be(revision);
        changes.Should().Be(0);
        studio.Preview.Should().BeSameAs(layer);
        studio.EndInteraction(true);
        changes.Should().Be(1);
        studio.Session.Revision.Should().Be(revision + 1);
        studio.Session.Draft.TextX.Should().Be(39);
        studio.UndoCommand.Execute(null);
        studio.Session.State.Should().BeNull();
        await studio.RenderNowAsync();
        studio.BeginInteraction().Should().BeTrue();
        studio.UpdateInteraction(studio.LiveDocument with { TextX = 50 });
        studio.EndInteraction(false);
        studio.Session.State.Should().BeNull();
        studio.LiveDocument.TextX.Should().Be(0);
    }

    [Fact]
    public async Task TextEdit_ReusesDecodedBackgroundAndUploadedTexture() {
        using var studio = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "标题", _ => { });
        var bytes = SkiaFixture.EncodeSolid(400, 300, 30, 80, 120, 255, "png");
        var asset = await studio.Session.Assets.ImportAsync(bytes);
        studio.Session.Change(studio.Session.Draft with { Background = asset, TemplateId = "photo" });
        await studio.RenderNowAsync();
        studio.HasPreview.Should().BeTrue(studio.StatusMessage);
        var texture = studio.Preview!.Background;
        var data = studio.Preview.Data.Background;
        studio.CoverTitle = "更新标题";
        await studio.RenderNowAsync();
        studio.Preview!.Background.Should().BeSameAs(texture);
        studio.Preview.Data.Background.Should().BeSameAs(data);
        studio.Session.Assets.CacheDirectory.Should().NotBeNullOrEmpty();
    }
    [Fact]
    public async Task SelectArticleBackground_ComposesLocalImageAndCancelsPendingRandomBackground() {
        var path = Path.Combine(Path.GetTempPath(), $"article-image-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, SkiaFixture.EncodeSolid(48, 32, 0, 170, 0, 255, "png"));
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken randomToken = default;
        var factory = Factory((_, token) => { randomToken = token; return pending.Task; });
        PreparedWeChatCover? applied = null;
        using var studio = new CoverStudioViewModel(factory, "", cover => applied = cover,
            [new ArticleImageSource(path, "文章配图")]) { Scrim = 0 };
        try {
            studio.Start();
            var download = studio.PickRandomBackgroundCommand.ExecuteAsync(null);
            await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[0]);
            randomToken.IsCancellationRequested.Should().BeTrue();
            studio.IsFetchingRandomBackground.Should().BeFalse();
            studio.IsFetchingArticleBackground.Should().BeFalse();
            studio.Session.Draft.Background.Should().NotBeNull();
            await studio.ApplyToWeChatCommand.ExecuteAsync(null);
            var cover = SkiaFixture.Sample(studio.OutputPath, 600, 450);
            cover.Width.Should().Be(1200);
            cover.Height.Should().Be(900);
            cover.G.Should().BeGreaterThan(150);
            cover.R.Should().BeLessThan(20);

            await studio.ApplyToWeChatCommand.ExecuteAsync(null);
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
        var bytes = SkiaFixture.EncodeSolid(48, 32, 0, 0, 200, 255, "png");
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
        ]) { Scrim = 0 };
        studio.Start();
        await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[0]);
        await studio.ApplyToWeChatCommand.ExecuteAsync(null);
        var previous = studio.OutputPath;
        SkiaFixture.Sample(previous, 600, 450).B.Should().BeGreaterThan(180);
        requested.Should().Contain(new Uri("https://images.example/photo?id=1&size=2"));

        await studio.SelectArticleBackgroundCommand.ExecuteAsync(studio.ArticleImages[1]);

        studio.OutputPath.Should().Be(previous);
        studio.Session.Draft.Background.Should().NotBeNull();
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
}
