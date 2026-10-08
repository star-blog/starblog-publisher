using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.DesktopTests;

internal sealed partial class WorkspaceScenarios {
    public async Task CoverStudio() {
        var sources = new List<ArticleImageSource>();
        foreach (var (name, color) in new[] {
                     ("绿色的本地配图", new Rgba32(20, 160, 80)),
                     ("蓝色的在线配图", new Rgba32(40, 80, 190))
                 }) {
            var path = Path.Combine(output, name + ".png");
            using var image = new Image<Rgba32>(600, 400, color);
            await image.SaveAsPngAsync(path);
            sources.Add(new ArticleImageSource(path, name));
        }
        var onlineBytes = await File.ReadAllBytesAsync(sources[1].Source);
        sources[1] = sources[1] with { Source = "https://images.example/article.png" };
        sources.Add(new ArticleImageSource(Path.Combine(output, "missing.png"), "缺失的配图"));
        var factory = new CoverImageHttpClientFactory(onlineBytes);
        var parent = new WeChatViewModel(factory);
        shell.ActivePage = parent;
        var studio = new CoverStudioViewModel(factory, "从文章图片制作公众号封面", cover => {
            parent.UsePreparedCover(cover, "文章图片制作封面");
            parent.NavigateBreadcrumbAt(0);
        }, sources);
        parent.OpenStackPage(studio, studio.Title);
        await WaitUntilAsync(() => Task.FromResult(studio.HasPreview), "Cover preview did not load");
        var view = window.GetVisualDescendants().OfType<CoverStudioView>().Single();
        var picker = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, studio.ShowArticleImagesCommand));
        await studio.ShowArticleImagesCommand.ExecuteAsync(picker.CommandParameter);
        await WaitUntilAsync(() => Task.FromResult(studio.ArticleImages.All(image => image.Status != "正在加载…")), "Article thumbnails did not finish loading");
        if (!studio.IsArticleImagePickerOpen || studio.ArticleImages.Take(2).Any(image => image.Thumbnail == null))
            throw new Exception("Local/remote article thumbnails did not display");
        if (!studio.ArticleImages[2].Status.Contains("失败")) throw new Exception("Missing thumbnail has no error state");
        Capture(window, Path.Combine(output, "cover-article-picker.png"));
        foreach (var width in new[] { 1000d, 1280d }) {
            window.Width = width;
            await Task.Delay(100);
            Capture(window, Path.Combine(output, $"cover-article-picker-{width}.png"));
        }

        var choice = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.CommandParameter, studio.ArticleImages[0]));
        await studio.SelectArticleBackgroundCommand.ExecuteAsync(choice.CommandParameter);
        await WaitUntilAsync(() => Task.FromResult(!studio.IsFetchingArticleBackground && studio.BackgroundDescription.Contains("绿色")), "Article image was not selected");
        if (studio.IsArticleImagePickerOpen || !studio.HasPreview) throw new Exception("Choosing an article image did not return to preview");
        Capture(window, Path.Combine(output, "cover-article-selected.png"));
        var outputPath = studio.OutputPath;
        var apply = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, studio.ApplyToWeChatCommand));
        apply.Command!.Execute(apply.CommandParameter);
        if (parent.IsStackNavigating || parent.CoverPath != outputPath || !File.Exists(outputPath))
            throw new Exception("Composed article cover was not applied or was deleted on navigation");
        parent.CoverPreview?.Dispose();
        File.Delete(outputPath);

        using var empty = new CoverStudioViewModel(factory, "无图文章", _ => { });
        parent.OpenStackPage(empty, empty.Title);
        await Task.Delay(150);
        view = window.GetVisualDescendants().OfType<CoverStudioView>().Single();
        picker = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, empty.ShowArticleImagesCommand));
        if (picker.IsEnabled || !view.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "文章正文中没有可用图片" && text.IsEffectivelyVisible))
            throw new Exception("No-image article has no disabled picker and empty-state hint");
        parent.NavigateBreadcrumbAt(0);
    }

    private sealed class CoverImageHttpClientFactory(byte[] bytes) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(new Handler(bytes));
        private sealed class Handler(byte[] bytes) : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
