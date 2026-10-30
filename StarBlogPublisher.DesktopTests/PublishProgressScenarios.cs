using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;
using StarBlogPublisher.Views.Controls;

namespace StarBlogPublisher.DesktopTests;

internal sealed partial class WorkspaceScenarios {
    public async Task PublishProgress() {
        // Simulate long-running callbacks without publishing to a real site.
        var path = Path.Combine(output, "publish-progress.md");
        await File.WriteAllTextAsync(path, "# 发布进度回归\n\n" + string.Join("\n\n",
            Enumerable.Range(1, 30).Select(i => $"## 章节 {i}\n\n预览保持可见，发布进度显示在独立区域。")));
        await shell.Workspace.OpenPathAsync(path);
        var document = shell.PublishPage;
        document.ArticleTitle = "发布进度与图片上传";
        document.ArticleDescription = "发布过程中可查看正文，完成后恢复编辑。";
        document.IsInspectorOpen = true;
        shell.Workspace.IsSidebarOpen = false;
        shell.ActivePage = shell.Workspace;
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<TextEditor>().Any()),
            "Article editor was not attached");
        var view = window.GetVisualDescendants().OfType<PublishEditorView>().Single();
        var progress = view.GetVisualDescendants().OfType<PublishProgressView>().Single();
        var overlay = view.GetVisualDescendants().OfType<BusyOverlay>().Single();
        var source = view.GetVisualDescendants().OfType<TextEditor>().Single();
        var inspector = view.GetVisualDescendants().OfType<ArticleInspectorView>().Single();
        if (progress.IsVisible || overlay.IsVisible) throw new Exception("Idle workspace shows progress or dimming");
        var withWebView = Environment.GetCommandLineArgs().Contains("--webview");
        var modes = withWebView
            ? new[] { MarkdownEditorMode.Source, MarkdownEditorMode.Split, MarkdownEditorMode.Preview }
            : new[] { MarkdownEditorMode.Source };
        if (!withWebView) Console.WriteLine("SKIP native preview publication checks: requires --webview");

        foreach (var dark in new[] { false, true }) {
            window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            foreach (var width in new[] { 800d, 1280d }) {
                window.Width = width;
                foreach (var mode in modes) {
                    document.EditorMode = mode;
                    NativeWebView? browser = null;
                    if (mode != MarkdownEditorMode.Source) {
                        browser = await WaitForPreviewBrowserAsync();
                        await WaitUntilAsync(async () => {
                            try { return await browser.InvokeScript("!!document.querySelector('[data-outline-line]')") == "true"; }
                            catch (InvalidOperationException) { return false; }
                        }, "Publishing preview did not load", 15000);
                    }
                    document.PublishProgress = 0;
                    document.StatusMessage = "正在检查登录状态...";
                    document.IsPublishing = true;
                    foreach (var (value, message) in new[] {
                        (0, "正在检查登录状态..."), (10, "正在创建文章..."),
                        (30, "正在上传图片（已完成 0/12）..."), (55, "正在上传图片（已完成 6/12）..."),
                        (90, "正在获取文章详情...")
                    }) {
                        document.PublishProgress = value;
                        document.StatusMessage = message;
                        await Task.Delay(60);
                        if (!progress.IsEffectivelyVisible || progress.Bounds.Height < 80 || overlay.IsVisible)
                            throw new Exception("Publishing feedback is missing or still dims the workspace");
                        if (progress.FindControl<TextBlock>("PublishPercentage")!.Text != $"{value}%"
                            || progress.FindControl<TextBlock>("PublishStatus")!.Text != message
                            || progress.FindControl<ProgressBar>("PublishProgressBar")!.Value != value)
                            throw new Exception("Progress bindings do not follow service callbacks");
                        var origin = progress.TranslatePoint(default, window)!.Value;
                        foreach (var label in progress.GetVisualDescendants().OfType<TextBlock>()) {
                            var position = label.TranslatePoint(default, window)!.Value;
                            if (position.X < origin.X - 1 || position.X + label.Bounds.Width > origin.X + progress.Bounds.Width + 1)
                                throw new Exception("Publishing feedback overflows the narrow workspace");
                        }
                        if (!source.IsReadOnly || inspector.IsEffectivelyEnabled)
                            throw new Exception("Article can be changed during publication");
                        if (browser != null) {
                            var browserTop = browser.TranslatePoint(default, window)!.Value.Y;
                            if (!browser.IsEffectivelyVisible || browserTop < origin.Y + progress.Bounds.Height)
                                throw new Exception("Native preview overlaps or hides the progress panel");
                        }
                        if (value == 55) Capture(window, Path.Combine(output, $"publish-{mode}-{width}-{(dark ? "dark" : "light")}.png"));
                    }
                    if (!dark && width == 1280 && mode == MarkdownEditorMode.Preview
                        && Environment.GetEnvironmentVariable("STARBLOG_PUBLISH_PROGRESS_MANUAL_TEST") == "1") {
                        document.PublishProgress = 55;
                        document.StatusMessage = "正在上传图片（已完成 6/12）...";
                        Console.WriteLine("Publication progress ready for native-window inspection (45 seconds).");
                        await Task.Delay(45000);
                    }
                    // Both service success and failure clear IsPublishing in the command's finally block.
                    document.IsPublishing = false;
                    document.StatusMessage = "发布失败：模拟网络错误";
                    await Task.Delay(60);
                    if (progress.IsVisible || source.IsReadOnly || !inspector.IsEffectivelyEnabled)
                        throw new Exception("Publication completion did not restore editing");
                }
            }
        }

        // The progress belongs to its document; switching tabs must not leak the lock or progress.
        document.IsPublishing = true;
        shell.Workspace.NewDocumentCommand.Execute(null);
        await Task.Delay(80);
        if (window.GetVisualDescendants().OfType<PublishProgressView>().Any(p => p.IsEffectivelyVisible)
            || window.GetVisualDescendants().OfType<TextEditor>().Any(e => e.IsEffectivelyVisible && e.IsReadOnly))
            throw new Exception("Publishing state leaked into another document");
        shell.Workspace.ActiveDocument = document;
        await Task.Delay(80);
        if (!window.GetVisualDescendants().OfType<PublishProgressView>().Any(p => p.IsEffectivelyVisible))
            throw new Exception("Switching back lost the publishing feedback");
        document.IsPublishing = false;
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Width = 1280;
        shell.Workspace.IsSidebarOpen = true;
        document.EditorMode = MarkdownEditorMode.Source;
    }
}
