using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        }, sources) { Library = new CoverTemplateLibrary(Path.Combine(output, "cover-library")) };
        parent.OpenStackPage(studio, studio.Title);
        shell.Workspace.CurrentDocument.OpenStackPage(studio, studio.Title);
        await WaitUntilAsync(() => Task.FromResult(studio.HasPreview), "Cover preview did not load");
        var view = window.GetVisualDescendants().OfType<CoverStudioView>().Single();
        studio.IsInspectorOpen = true; studio.SelectObjectCommand.Execute(CoverElementId.Background);
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
        window.Width = 1000; await Task.Delay(100);
        studio.IsInspectorOpen = false; await Task.Delay(50);
        var canvasBeforeDrawer = view.GetVisualDescendants().OfType<StarBlogPublisher.Views.Controls.CoverCanvas>().Single(c => c.Frame == CoverPreviewFrame.Full).Bounds.Width;
        studio.IsInspectorOpen = true; await Task.Delay(50);
        var canvasWithDrawer = view.GetVisualDescendants().OfType<StarBlogPublisher.Views.Controls.CoverCanvas>().Single(c => c.Frame == CoverPreviewFrame.Full).Bounds.Width;
        if (!studio.IsNarrow || Math.Abs(canvasBeforeDrawer - canvasWithDrawer) > 1) throw new Exception("Narrow drawer reduced the canvas width");
        Capture(window, Path.Combine(output, "cover-inspector-drawer.png"));
        window.Width = 1280; await Task.Delay(100);
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
        var revision = studio.Session.Revision;
        await studio.OpenTemplateLibraryCommand.ExecuteAsync(null);
        if (studio.VisibleTemplateCards.Count != 4 || studio.VisibleTemplateCards.Any(c => c.Thumbnail == null)) throw new Exception("Template gallery did not render four previews");
        if (studio.Session.Revision != revision) throw new Exception("Opening the template gallery changed the article");
        Capture(window, Path.Combine(output, "cover-template-gallery.png"));
        studio.CloseTemplateLibraryCommand.Execute(null);
        studio.SelectTemplateCommand.Execute(CoverTemplate.All.Single(t => t.Id == "photo"));
        studio.CoverTitle = "封面工作室";
        await studio.RenderNowAsync();
        Capture(window, Path.Combine(output, "cover-studio-final.png"));
        studio.StartInlineEdit(CoverElementId.Title);
        await Task.Delay(100);
        var editorCanvas = view.GetVisualDescendants().OfType<StarBlogPublisher.Views.Controls.CoverCanvas>().Single(c => c.Frame == CoverPreviewFrame.Full);
        var inline = editorCanvas.GetVisualDescendants().OfType<TextBox>().Single();
        if (!inline.IsVisible || inline.Bounds.Width <= 0 || !inline.IsFocused) throw new Exception("Inline text editor did not receive focus or bounds");
        studio.EndInlineEdit();
        foreach (var dark in new[] { false, true }) foreach (var lightText in new[] { true, false }) {
            window.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
            studio.SelectTemplateCommand.Execute(CoverTemplate.All.Single(t => t.Id == (lightText ? "photo" : "minimal")));
            studio.CoverTitle = "封面编辑";
            await studio.RenderNowAsync(); await Task.Delay(60);
            var textBox = studio.Preview!.Data.Layout.Objects.Single(o => o.Id == CoverElementId.Title).TextBox!.Value;
            var editorRect = editorCanvas.MasterRect(textBox);
            var sample = new PixelRect((int)(editorRect.Right - 8), (int)(editorRect.Bottom - 12), 1, 1);
            var before = CoverPixels(editorCanvas, sample);
            var cropViews = view.GetVisualDescendants().OfType<StarBlogPublisher.Views.Controls.CoverCanvas>()
                .Where(c => c.Frame != CoverPreviewFrame.Full && c.IsEffectivelyVisible).ToArray();
            var cropHashes = cropViews.Select(c => SHA256.HashData(CoverPixels(c))).ToArray();
            studio.StartInlineEdit(CoverElementId.Title); await Task.Delay(60);
            var during = CoverPixels(editorCanvas, sample);
            if (!before.SequenceEqual(during)) throw new Exception($"Focused cover editor painted over the background (dark={dark}, lightText={lightText})");
            if (inline.Foreground is not ISolidColorBrush ink || ink.Color != studio.SelectedColor) throw new Exception("Inline editor changed the design text color");
            if (inline.CaretBrush is not ISolidColorBrush caret || caret.Color != studio.SelectedColor) throw new Exception("Inline caret did not follow the design text color");
            for (var i = 0; i < cropViews.Length; i++)
                if (!cropHashes[i].SequenceEqual(SHA256.HashData(CoverPixels(cropViews[i])))) throw new Exception("Editing title removed it from a publication preview");
            Capture(window, Path.Combine(output, $"cover-inline-{(dark ? "dark" : "light")}-{(lightText ? "white" : "ink")}.png"));
            studio.EndInlineEdit();
        }
        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        studio.SelectTemplateCommand.Execute(CoverTemplate.All.Single(t => t.Id == "photo"));
        await studio.RenderNowAsync();
        studio.StartInlineEdit(CoverElementId.Title); await Task.Delay(60);
        inline.Text = "原位编辑：中文标题";
        studio.EndInlineEdit();
        await studio.RenderNowAsync();
        if (studio.CoverTitle != "原位编辑：中文标题") throw new Exception("Inline and sidebar content diverged");
        studio.SelectObjectCommand.Execute(CoverElementId.Title);
        Capture(window, Path.Combine(output, "cover-object-properties.png"));
        await studio.AdjustHeadlineCommand.ExecuteAsync(null);
        Capture(window, Path.Combine(output, "cover-crop-mode.png"));
        studio.CancelCompositionCommand.Execute(null);
        studio.PresetName = "桌面验收栏目"; studio.PresetIsColumn = true; studio.PresetIncludeBackground = true;
        var beforePreset = studio.Session.Draft; var beforeSaveRevision = studio.Session.Revision;
        await studio.SavePresetCommand.ExecuteAsync(null);
        if (studio.Session.Revision != beforeSaveRevision || studio.SelectedTemplateCard?.Preset?.IsColumn != true) throw new Exception("Saving a preset changed the article or did not select the saved entry");
        studio.IsTemplateLibraryOpen = true;
        await Task.Delay(150);
        var savedCard = studio.SelectedTemplateCard!;
        var savedButton = view.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => ReferenceEquals(b.CommandParameter, savedCard) && b.IsEffectivelyVisible);
        var useTemplateButton = view.GetVisualDescendants().OfType<Button>().Single(b => ReferenceEquals(b.Command, studio.UseTemplateCommand));
        if (savedButton == null || !useTemplateButton.IsEnabled) throw new Exception("Saved personal template is not visible or usable in the gallery");
        Capture(window, Path.Combine(output, "cover-personal-template.png"));
        await studio.UseTemplateCommand.ExecuteAsync(null);
        if (studio.CoverTitle != "从文章图片制作公众号封面：中文标题与裁剪验收" || studio.Subtitle != "") throw new Exception("Personal template did not use the destination article title");
        studio.UndoCommand.Execute(null);
        if (studio.Session.Draft != beforePreset) throw new Exception("Template use did not undo in one step");
        studio.SelectObjectCommand.Execute(CoverElementId.None);
        if (Environment.GetEnvironmentVariable("STARBLOG_COVER_MANUAL_TEST") == "1") {
            studio.DragTarget = "文字";
            Console.WriteLine("Cover studio ready for 180 seconds of manual pointer/keyboard inspection.");
            await Task.Delay(TimeSpan.FromSeconds(180));
        }

        var apply = view.GetVisualDescendants().OfType<Button>()
            .Single(button => ReferenceEquals(button.Command, studio.ApplyToWeChatCommand));
        await studio.ApplyToWeChatCommand.ExecuteAsync(apply.CommandParameter);
        var outputPath = studio.OutputPath;
        if (!parent.IsStackNavigating || parent.CoverPath != outputPath || !File.Exists(outputPath))
            throw new Exception("Composed article cover was not applied or was deleted on navigation");
        parent.CoverPreview?.Dispose();
        parent.CoverPreview = null;
        File.Delete(outputPath);

        using var empty = new CoverStudioViewModel(factory, "无图文章", _ => { });
        parent.OpenStackPage(empty, empty.Title);
        shell.Workspace.CurrentDocument.OpenStackPage(empty, empty.Title);
        empty.IsInspectorOpen = true; empty.SelectObjectCommand.Execute(CoverElementId.Background);
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

    private static byte[] CoverPixels(Control control, PixelRect? sample = null) {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);
        var rect = sample ?? new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var bytes = new byte[rect.Width * rect.Height * 4];
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { bitmap.CopyPixels(rect, pinned.AddrOfPinnedObject(), bytes.Length, rect.Width * 4); }
        finally { pinned.Free(); }
        return bytes;
    }

    private sealed class CoverImageHttpClientFactory(byte[] bytes) : IHttpClientFactory {
        public HttpClient CreateClient(string name) => new(new Handler(bytes));
        private sealed class Handler(byte[] bytes) : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
