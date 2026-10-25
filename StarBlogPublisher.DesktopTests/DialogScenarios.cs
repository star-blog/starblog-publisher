using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using FluentAvalonia.UI.Controls;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.DesktopTests;

internal sealed class DialogScenarios(MainWindow window, MainWindowViewModel shell, string output) {
    public static readonly string[] Cases = ["lifecycle", "save", "discard", "clean"];

    // Actual window-close cases cannot share a shell. Run each in its own disposable process.
    public static async Task<int> RunIsolatedCases() {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var suite = Path.Combine(root, "output", "desktop-tests", $"dialogs-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(suite);
        Console.WriteLine($"Dialog suite: {suite}");
        var results = new List<object>();
        var failed = false;
        foreach (var name in Cases) {
            var start = new ProcessStartInfo(Environment.ProcessPath!) {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--dialog-case=" + name);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch dialog case");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var watch = Stopwatch.StartNew();
            var timedOut = false;
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(120)); }
            catch (TimeoutException) {
                timedOut = true;
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            var log = await stdout + Environment.NewLine + await stderr;
            await File.WriteAllTextAsync(Path.Combine(suite, name + ".log"), log);
            var passed = !timedOut && process.ExitCode == 0;
            failed |= !passed;
            results.Add(new { name, status = passed ? "passed" : "failed", timedOut, durationMs = watch.ElapsedMilliseconds, process.ExitCode });
            await File.WriteAllTextAsync(Path.Combine(suite, "report.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{(passed ? "PASS" : "FAIL")} dialogs-{name}\n{log}");
        }
        return failed ? 1 : 0;
    }

    public async Task RunAsync(string name) {
        if (name == "clean") {
            await LoadArticle("clean.md");
            shell.NavigateToWeChat();
            await LoadedBrowser(window);
            window.Close();
            await Until(() => !window.IsVisible, "Clean shell did not close");
            return;
        }
        if (name is "save" or "discard") { await CloseShell(name == "save"); return; }
        await Lifecycle();
    }

    private async Task<string> LoadArticle(string name) {
        var path = Path.Combine(output, name);
        await File.WriteAllTextAsync(path, "# 弹窗回归测试\n\n" + string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"## 章节 {i}\n\n用于验证弹窗后的网页内容和滚动位置。")));
        await shell.Workspace.OpenPathAsync(path);
        return path;
    }

    private async Task CloseShell(bool save) {
        var paths = new[] { await LoadArticle("first.md"), await LoadArticle("second.md") };
        foreach (var document in shell.Workspace.Documents) document.ArticleContent += "\n\n未保存的修改";
        shell.NavigateToWeChat();
        var browser = await LoadedBrowser(window);
        window.Close();
        var dialog = await CurrentDialog();
        Check(!browser.IsVisible, "Close confirmation left native browser visible");
        // A second click on the title-bar close button follows the same Close event path.
        window.Close();
        Check(Dialogs().Length == 1, "Repeated close opened another confirmation");
        Click(dialog, "取消");
        await Until(() => Dialogs().Length == 0 && browser.IsVisible, "Cancel did not restore browser");
        Check(window.IsVisible && shell.Workspace.Documents.All(d => d.IsDocumentDirty), "Cancel lost the shell or dirty documents");
        await Task.Delay(100);
        window.Close();
        foreach (var path in paths) {
            dialog = await CurrentDialog();
            Check(dialog.Title?.ToString()?.Contains(Path.GetFileName(path)) == true, "Close skipped a dirty article");
            Check(!browser.IsVisible, "Browser became visible between close confirmations");
            Click(dialog, save ? "保存" : "不保存");
            await Until(() => !Dialogs().Contains(dialog), "Close confirmation did not finish");
        }
        await Until(() => !window.IsVisible, "Confirmed shell did not close");
        foreach (var path in paths) {
            Check((await File.ReadAllTextAsync(path)).Contains("未保存的修改") == save, "Save/discard produced wrong file contents");
            if (save) Check(File.Exists(StarBlogPublisher.Models.ArticleSidecar.PathFor(path)), "Save did not persist article properties");
        }
    }

    private async Task Lifecycle() {
        await LoadArticle("dialogs.md");
        shell.NavigateToWeChat();
        var browser = await LoadedBrowser(window);
        await browser.InvokeScript("window.__dialogProbe=123; window.scrollTo(0,450)");
        var scroll = await browser.InvokeScript("window.scrollY");
        await CheckOverlay(browser);
        Check(await browser.InvokeScript("window.__dialogProbe") == "123", "Dialog recreated the browser document");
        Check(await browser.InvokeScript("window.scrollY") == scroll, "Dialog lost browser scroll position");
        Console.WriteLine("PASS overlay-and-scroll");

        browser.IsVisible = false;
        await CheckOverlay(browser, restoredVisible: false);
        browser.ClearValue(Visual.IsVisibleProperty);
        await BindingsAndNestedDialogs(browser);
        Console.WriteLine("PASS bindings-and-nesting");
        await FailureAndOwnerCleanup(browser);
        Console.WriteLine("PASS failures-and-owner-cleanup");

        var palette = shell.PaletteRequested!(true);
        var dialog = await CurrentDialog();
        Check(!browser.IsVisible && dialog.Title?.ToString() == "命令面板", "Palette bypassed dialog host");
        Click(dialog, "取消");
        await palette;
        var prompt = GuiHost.PromptAsync("输入测试", "原值");
        dialog = await CurrentDialog();
        dialog.GetVisualDescendants().OfType<TextBox>().Single().Text = "修改值";
        Click(dialog, "确定");
        Check(await prompt == "修改值" && browser.IsVisible, "Prompt lost its result or browser restoration");
        var taskDialog = new FATaskDialog { Header = "Task dialog" };
        taskDialog.Buttons.Add(new FATaskDialogButton("确定", FATaskDialogStandardResult.OK));
        var taskResult = GuiHost.ShowDialogAsync(taskDialog);
        await Until(() => taskDialog.IsAttachedToVisualTree(), "Task dialog did not open");
        Check(!browser.IsVisible, "Task dialog bypassed host");
        taskDialog.Hide(FATaskDialogStandardResult.OK);
        Check(await taskResult is FATaskDialogStandardResult.OK && browser.IsVisible, "Task dialog result changed");
        var content = new DialogContentProbe();
        for (var i = 0; i < 2; i++) {
            var shown = GuiHost.ShowContentAsync(content, "复用内容弹窗");
            await CurrentDialog();
            content.RequestClose();
            await shown;
        }
        content.RequestClose();
        await Task.Delay(250);
        Check(browser.IsVisible, "Content dialog leaked its close subscription");
        Console.WriteLine("PASS palette-prompt-task-dialog");

        shell.ActivePage = shell.Workspace;
        foreach (var mode in new[] { MarkdownEditorMode.Preview, MarkdownEditorMode.Split }) {
            shell.PublishPage.EditorMode = mode;
            browser = await LoadedBrowser(window);
            await CheckOverlay(browser);
        }
        shell.PublishPage.EditorMode = MarkdownEditorMode.Source;
        await Task.Delay(100);
        await CheckOverlay(browser);
        Check(!browser.IsEffectivelyVisible, "Source mode unexpectedly showed preview");
        var editor = window.GetVisualDescendants().OfType<TextEditor>().First(e => e.IsEffectivelyVisible);
        editor.TextArea.Focus();
        var previousFocus = window.FocusManager!.GetFocusedElement();
        var find = shell.EditRequested!("find");
        dialog = await CurrentDialog();
        Click(dialog, "关闭");
        await find;
        Check(window.FocusManager.GetFocusedElement() == previousFocus, "Find did not restore editor focus");
        Console.WriteLine("PASS local-modes-and-focus");

        var settings = shell.SettingsPage;
        settings.Username += "dialog-test";
        settings.ConfirmLeaveOverride = () => throw new InvalidOperationException("expected close failure");
        window.Close();
        await Until(() => window.FindControl<FAInfoBar>("FeedbackBar")?.Message == "expected close failure", "Close failure did not report an error");
        Check(window.IsVisible, "Close failure lost the window");
        settings.ConfirmLeaveOverride = null;
        window.Close();
        dialog = await CurrentDialog();
        Click(dialog, "继续编辑");
        await Until(() => Dialogs().Length == 0, "Close retry did not complete");
        Check(window.IsVisible && settings.HasChanges, "Settings cancel lost the draft");
        var leave = settings.CanLeaveAsync();
        dialog = await CurrentDialog();
        Click(dialog, "放弃更改");
        Check(await leave && !settings.HasChanges, "Settings discard failed");
        Console.WriteLine("PASS settings-confirmation");

        var api = ApiService.Instance;
        var auth = new AuthApplicationService(AppSettings.Instance, GlobalState.Instance, api);
        var online = new SitePostEditorViewModel(shell, new ArticleLibraryApplicationService(api, auth),
            new CategoryApplicationService(api, auth), "dialog-preview", _ => { }) {
            Title = "站点文章", Content = "# 站点预览\n\n本地测试内容", IsLoaded = true, EditorMode = MarkdownEditorMode.Preview
        };
        shell.ActivePage = shell.SitePostsPage;
        shell.SitePostsPage.OpenStackPage(online, "编辑文章");
        browser = await LoadedBrowser(window);
        await CheckOverlay(browser);
        Console.WriteLine("PASS site-posts-preview");
        shell.ActivePage = shell.Workspace;
        window.Close();
        await Until(() => !window.IsVisible, "Lifecycle shell did not close");
    }

    private async Task BindingsAndNestedDialogs(NativeWebView browser) {
        var source = new Border { IsVisible = true };
        using var binding = browser.Bind(Visual.IsVisibleProperty, new Binding(nameof(Visual.IsVisible)) { Source = source });
        var outer = NewDialog("outer");
        var outerResult = GuiHost.ShowDialogAsync(outer);
        await CurrentDialog();
        var ownBrowser = new NativeWebView { Width = 80, Height = 30 };
        var inner = NewDialog("inner");
        inner.Content = ownBrowser;
        var innerResult = GuiHost.ShowDialogAsync(inner);
        await Until(() => Dialogs().Length == 2, "Nested dialog did not open");
        await LoadedBrowser(inner);
        Check(ownBrowser.IsVisible && !browser.IsVisible, "Dialog content browser was suppressed");
        var root = (Panel)window.Content!;
        var added = new NativeWebView { Width = 5, Height = 5, IsVisible = true };
        root.Children.Add(added);
        await Until(() => !added.IsVisible, "New background browser escaped suppression");
        source.IsVisible = false;
        Click(inner, "取消");
        await innerResult;
        Check(!browser.IsVisible && !added.IsVisible, "Inner dialog restored background too soon");
        Click(outer, "取消");
        await outerResult;
        Check(!browser.IsVisible && added.IsVisible, "Dialog did not restore latest binding/local values");
        source.IsVisible = true;
        Check(browser.IsVisible, "Original visibility binding was replaced");
        root.Children.Remove(added);
    }

    private async Task FailureAndOwnerCleanup(NativeWebView browser) {
        var failed = NewDialog("failure");
        failed.Opening += (_, _) => throw new InvalidOperationException("expected dialog failure");
        try { await GuiHost.ShowDialogAsync(failed); throw new Exception("Expected show failure"); }
        catch (InvalidOperationException ex) when (ex.Message == "expected dialog failure") { }
        Check(browser.IsVisible, "Show failure leaked visibility override");
        await CheckOverlay(browser);

        var otherBrowser = new NativeWebView { Width = 80, Height = 30 };
        var other = new Window { Width = 300, Height = 200, Content = otherBrowser };
        other.Show();
        try {
            await LoadedBrowser(other);
            var primaryDialog = NewDialog("primary");
            var primaryResult = GuiHost.ShowDialogAsync(primaryDialog);
            await CurrentDialog();
            Check(otherBrowser.IsVisible, "Dialog suppressed a different window");
            var secondaryDialog = NewDialog("secondary");
            var secondaryResult = GuiHost.ShowDialogAsync(secondaryDialog, other);
            await Until(() => !otherBrowser.IsVisible, "Explicit owner browser was not hidden");
            Click(primaryDialog, "取消");
            await primaryResult;
            Check(browser.IsVisible && !otherBrowser.IsVisible, "Per-window dialog counts interfered");
            other.Close();
            Check(await secondaryResult == FAContentDialogResult.None, "Closed owner did not cancel its dialog");
            Check(otherBrowser.IsVisible, "Owner close leaked visibility override");
        }
        finally { other.Close(); }
    }

    private async Task CheckOverlay(NativeWebView browser, bool restoredVisible = true) {
        var dialog = NewDialog("覆盖测试");
        var result = GuiHost.ShowDialogAsync(dialog);
        await CurrentDialog();
        Check(!browser.IsVisible, "Overlay left background browser visible");
        Click(dialog, "取消");
        Check(await result == FAContentDialogResult.None, "Dialog cancel result changed");
        Check(browser.IsVisible == restoredVisible, "Dialog did not restore browser visibility");
    }

    private static FAContentDialog NewDialog(string title) => new() { Title = title, Content = "弹窗测试", CloseButtonText = "取消" };
    private FAContentDialog[] Dialogs() => window.GetVisualDescendants().OfType<FAContentDialog>().ToArray();
    private async Task<FAContentDialog> CurrentDialog() {
        await Until(() => Dialogs().Any(d => d.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible)), "Dialog did not open");
        return Dialogs().Last();
    }
    private static void Click(FAContentDialog dialog, string label) => dialog.GetVisualDescendants().OfType<Button>()
        .Single(b => b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task<NativeWebView> LoadedBrowser(Visual root) {
        NativeWebView? browser = null;
        await Until(async () => {
            browser = root.GetVisualDescendants().OfType<NativeWebView>().FirstOrDefault(b => b.IsEffectivelyVisible)
                ?? root as NativeWebView;
            try {
                if (browser == null || await browser.InvokeScript("document.readyState") != "\"complete\"") return false;
                if (browser.Source == null) return true;
                var url = JsonSerializer.Deserialize<string>((await browser.InvokeScript("location.href"))!);
                return Uri.TryCreate(url, UriKind.Absolute, out var current) && current == browser.Source;
            }
            catch (InvalidOperationException) { return false; }
        }, "Native browser did not load", 15000);
        return browser!;
    }
    private static Task Until(Func<bool> condition, string message, int timeout = 5000) => Until(() => Task.FromResult(condition()), message, timeout);
    private static async Task Until(Func<Task<bool>> condition, string message, int timeout = 5000) {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeout) {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException(message);
    }

    private sealed class DialogContentProbe : IDialogHostAware {
        public event Action? CloseRequested;
        public void RequestClose() => CloseRequested?.Invoke();
    }
}
