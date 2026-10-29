using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SkiaSharp;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.DesktopTests;

internal sealed partial class WorkspaceScenarios {
    public async Task CoverStudio() {
        var sources = new List<ArticleImageSource>();
        foreach (var (name, color) in new[] {
                     ("绿色的本地配图", new SKColor(20, 160, 80)),
                     ("蓝色的在线配图", new SKColor(40, 80, 190))
                 }) {
            var path = Path.Combine(output, name + ".png");
            using var bitmap = new SKBitmap(new SKImageInfo(4000, 3000, SKColorType.Rgba8888, SKAlphaType.Opaque));
            bitmap.Erase(color);
            using (var canvas = new SKCanvas(bitmap)) {
                using var gradient = SKShader.CreateLinearGradient(new(0, 0), new(4000, 3000), [color, new SKColor(12, 35, 60)], SKShaderTileMode.Clamp);
                using var paint = new SKPaint { Shader = gradient };
                canvas.DrawRect(0, 0, 4000, 3000, paint);
                using var circle = new SKPaint { Color = new SKColor(255, 255, 255, 20), IsAntialias = true };
                canvas.DrawCircle(3200, 600, 1100, circle);
                canvas.DrawCircle(3600, 2500, 1500, circle);
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var file = File.Create(path);
            data.SaveTo(file);
            sources.Add(new ArticleImageSource(path, name));
        }
        var onlineBytes = await File.ReadAllBytesAsync(sources[1].Source);
        sources[1] = sources[1] with { Source = "https://images.example/article.png" };
        sources.Add(new ArticleImageSource(Path.Combine(output, "missing.png"), "缺失的配图"));
        var factory = new CoverImageHttpClientFactory(onlineBytes);
        var parent = new WeChatViewModel(factory);
        // Exercise the native editor without attaching the unrelated WebView2 formatting preview.
        var previousSidebar = shell.Workspace.IsSidebarOpen;
        shell.Workspace.IsSidebarOpen = false;
        var studio = new CoverStudioViewModel(factory, "从文章图片制作公众号封面：中文标题与裁剪验收", cover => {
            parent.UsePreparedCover(cover, "文章图片制作封面");
            parent.NavigateBreadcrumbAt(0);
        }, sources);
        parent.OpenStackPage(studio, studio.Title);
        shell.Workspace.CurrentDocument.OpenStackPage(studio, studio.Title);
        await WaitUntilAsync(() => Task.FromResult(studio.HasPreview), "Cover preview did not load");
        var view = window.GetVisualDescendants().OfType<CoverStudioView>().Single();
        studio.IsInspectorOpen = true; studio.InspectorTab = 1;
        await Task.Delay(100);
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
        await WaitUntilAsync(() => Task.FromResult(!studio.IsFetchingArticleBackground && studio.Session.Draft.Background != null), "Article image was not selected");
        if (studio.IsArticleImagePickerOpen || !studio.HasPreview) throw new Exception("Choosing an article image did not return to preview");
        Capture(window, Path.Combine(output, "cover-article-selected.png"));
        foreach (var width in new[] { 1000d, 1280d }) {
            window.Width = width; await Task.Delay(100);
            Capture(window, Path.Combine(output, $"cover-editor-{width}.png"));
        }
        foreach (var dark in new[] { false, true }) {
            window.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            foreach (var template in CoverTemplate.All) {
                studio.SelectTemplateCommand.Execute(template);
                await studio.RenderNowAsync();
                if (studio.HeadlinePreview == null || studio.SecondaryPreview == null) throw new Exception("Both crop previews are required");
                Capture(window, Path.Combine(output, $"cover-{template.Id}-{(dark ? "dark" : "light")}.png"));
            }
        }
        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        studio.SelectTemplateCommand.Execute(CoverTemplate.All.Single(t => t.Id == "photo"));
        studio.CoverTitle = "封面工作室";
        await studio.RenderNowAsync();
        Capture(window, Path.Combine(output, "cover-studio-final.png"));
        if (Environment.GetEnvironmentVariable("STARBLOG_COVER_MANUAL_TEST") == "1") {
            studio.DragTarget = "文字";
            Console.WriteLine("Cover studio ready for 90 seconds of manual pointer/keyboard inspection.");
            await Task.Delay(TimeSpan.FromSeconds(90));
        }

        var apply = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, studio.ApplyToWeChatCommand));
        await studio.ApplyToWeChatCommand.ExecuteAsync(apply.CommandParameter);
        var outputPath = studio.OutputPath;
        if (parent.IsStackNavigating || parent.CoverPath != outputPath || !File.Exists(outputPath))
            throw new Exception("Composed article cover was not applied or was deleted on navigation");
        parent.CoverPreview?.Dispose();
        parent.CoverPreview = null;
        File.Delete(outputPath);

        using var empty = new CoverStudioViewModel(factory, "无图文章", _ => { });
        parent.OpenStackPage(empty, empty.Title);
        shell.Workspace.CurrentDocument.OpenStackPage(empty, empty.Title);
        empty.IsInspectorOpen = true; empty.InspectorTab = 1;
        await Task.Delay(150);
        view = window.GetVisualDescendants().OfType<CoverStudioView>().Single();
        picker = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, empty.ShowArticleImagesCommand));
        if (picker.IsEnabled)
            throw new Exception("No-image article has no disabled picker and empty-state hint");
        parent.NavigateBreadcrumbAt(0);
        shell.Workspace.CurrentDocument.NavigateBreadcrumbAt(0);
        shell.Workspace.IsSidebarOpen = previousSidebar;
        shell.ActivePage = shell.Workspace;
        await Task.Delay(100);
    }

    private sealed class CoverImageHttpClientFactory(byte[] bytes) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(new Handler(bytes));
        private sealed class Handler(byte[] bytes) : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
