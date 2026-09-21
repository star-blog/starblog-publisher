using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;
using AvaloniaEdit;
using Avalonia.Input;
using StarBlogPublisher.Views.Controls;

namespace StarBlogPublisher.DesktopTests;

// Ordered stages of one real-window workflow; the runner skips later stages after failure.
internal sealed class WorkspaceScenarios(MainWindow window, MainWindowViewModel shell, string output) {
    private string one = "", two = "";
    private PublishViewModel first = null!;
    private TextEditor editor = null!;
    private Menu mainMenu = null!;

    public async Task SettingsLayout() {
        var before = typeof(SettingsViewModel).GetProperties().Where(p => p.PropertyType == typeof(string) || p.PropertyType == typeof(bool) || p.PropertyType == typeof(int))
            .ToDictionary(p => p.Name, p => p.GetValue(shell.SettingsPage));
        shell.ActivePage = shell.SettingsPage;
        await Task.Delay(200);
        var vm = shell.SettingsPage;
        if (vm.HasChanges) throw new Exception("Opening settings changed fields: " + string.Join(", ", before.Keys.Where(name => !Equals(before[name], typeof(SettingsViewModel).GetProperty(name)!.GetValue(vm)))));
        var view = window.GetVisualDescendants().OfType<SettingsView>().First();
        var navigation = view.FindControl<ListBox>("SettingsNavigation")!;
        var form = view.FindControl<StackPanel>("SettingsForm")!;
        var scroll = view.FindControl<ScrollViewer>("SettingsScrollViewer")!;
        var advanced = view.FindControl<Expander>("WeChatAdvanced")!;
        var originalWidth = form.Bounds.Width;
        foreach (var section in Enumerable.Range(0, 5)) {
            navigation.SelectedIndex = section;
            await Task.Delay(100);
            if (vm.SelectedSection != section) throw new Exception("Settings navigation binding did not update");
            if (Math.Abs(form.Bounds.Width - originalWidth) > 1) throw new Exception("Settings form width changed across categories");
            if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new Exception("Settings form overflows horizontally");
            Capture(window, Path.Combine(output, $"settings-{section}.png"));
        }
        navigation.SelectedIndex = 2;
        vm.WeChatAccountName = "桌面测试公众号";
        vm.UseWeChatRelay = true;
        vm.WeChatApiBaseUrl = "https://relay.example.com/";
        advanced.IsExpanded = true;
        await Task.Delay(150);
        if (Math.Abs(form.Bounds.Width - originalWidth) > 1) throw new Exception("Expanding advanced settings changed form width");
        var themeSelector = view.FindControl<ComboBox>("WeChatThemeSelector")!;
        if (themeSelector.SelectedValue?.ToString() != vm.WeChatDefaultTheme) throw new Exception("Theme label/value mapping failed");
        themeSelector.SelectedIndex = 1;
        if (vm.WeChatDefaultTheme != "warm-card") throw new Exception("Theme selection did not update persisted theme ID");
        navigation.SelectedIndex = 3;
        vm.AIKey = "desktop-test-placeholder";
        navigation.SelectedIndex = 2;
        if (vm.WeChatAccountName != "桌面测试公众号" || !vm.HasChanges) throw new Exception("Draft lost while changing categories");
        window.Width = 800;
        await Task.Delay(200);
        if (scroll.Extent.Width > scroll.Viewport.Width + 1 || form.Bounds.Width < 300) throw new Exception("Narrow settings layout is unusable");
        Capture(window, Path.Combine(output, "settings-narrow.png"));
        vm.IsDarkTheme = true;
        await Task.Delay(150);
        Capture(window, Path.Combine(output, "settings-dark.png"));
        vm.CancelCommand.Execute(null);
        if (vm.HasChanges) throw new Exception("Cancel did not clear settings draft");
        vm.Username = "leave-dialog-draft";
        shell.ActivePage = shell.AboutPage;
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<FluentAvalonia.UI.Controls.FAContentDialog>().Any()), "Settings leave dialog did not open");
        ClickDialog(window, "继续编辑");
        await Task.Delay(100);
        if (shell.ActivePage != vm || !vm.HasChanges) throw new Exception("Cancel navigation lost settings draft");
        vm.UseCustomBackend = true;
        vm.BackendUrl = "invalid-url";
        vm.SaveCommand.Execute(null);
        await Task.Delay(100);
        var invalidBox = view.GetVisualDescendants().OfType<TextBox>().First(box => box.Text == "invalid-url");
        if (!DataValidationErrors.GetHasErrors(invalidBox) || !vm.BlogNavigationLabel.Contains("待修正")) throw new Exception("Settings validation did not mark the failing field and category");
        Capture(window, Path.Combine(output, "settings-validation.png"));
        vm.CancelCommand.Execute(null);
        // A directory at the target file path makes the write fail without modifying the real configuration.
        var settingsPath = Environment.GetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH");
        var blockedPath = Path.Combine(output, "settings-write-blocked");
        Directory.CreateDirectory(blockedPath);
        var savedUsername = StarBlogPublisher.Services.AppSettings.Instance.Username;
        try {
            Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", blockedPath);
            vm.Username = "must-not-apply";
            vm.SaveCommand.Execute(null);
            if (!vm.HasChanges || !vm.HasValidationError || StarBlogPublisher.Services.AppSettings.Instance.Username != savedUsername)
                throw new Exception("Failed settings write applied or discarded the draft");
        }
        finally { Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", settingsPath); }
        vm.CancelCommand.Execute(null);
        vm.AIModel = "desktop-draft-model";
        await shell.OpenModelCatalogAsync();
        shell.ModelCatalogPage!.ReturnToSettingsCommand.Execute(null);
        await Task.Delay(100);
        if (vm.AIModel != "desktop-draft-model" || !vm.HasChanges || shell.ActivePage != vm) throw new Exception("Model catalog return lost settings draft");
        vm.CancelCommand.Execute(null);
        vm.Username = "desktop-test";
        vm.SaveCommand.Execute(null);
        if (vm.HasChanges || StarBlogPublisher.Services.AppSettings.Instance.Username != "desktop-test") throw new Exception("Settings save failed");
        vm.Username = "next-draft";
        vm.CancelCommand.Execute(null);
        if (vm.Username != "desktop-test") throw new Exception("Cancel did not restore saved settings");
        vm.Username = "";
        vm.SaveCommand.Execute(null);
        window.Width = 1280;
        shell.ActivePage = shell.Workspace;
        await Task.Delay(150);
    }

    public async Task WorkspaceState() {
        one = Path.Combine(output, "团队 Web 开发规范.md");
        two = Path.Combine(output, "公众号发布指南.md");
        await File.WriteAllTextAsync(one, "# 团队 Web 开发规范\n\n(DjangoStarter + Next.js)\n\n## 一、总体约定\n\n统一代码风格，保持前后端接口一致。\n\n" + string.Join("\n\n", Enumerable.Range(1, 40).Select(i => $"### {i}. 开发约定\n\n提交前运行格式化与 lint 检查。")));
        await File.WriteAllTextAsync(two, "# 公众号发布指南\n\n## 一、准备文章\n\n检查文章标题、摘要与封面。\n\n## 二、发送到草稿箱\n\n确认预览后发送。");
        await shell.Workspace.OpenPathAsync(one);
        first = shell.PublishPage;
        await Task.Delay(350);
        editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        editor.CaretOffset = 100;
        editor.ScrollToLine(35);
        editor.Document.Insert(0, "编辑测试\n");
        await Task.Delay(100);
        editor.ScrollToLine(35);
        await Task.Delay(100);
        Capture(window, Path.Combine(output, "initial.png"));
        if (editor.VerticalOffset < 100) throw new Exception($"Initial scroll was not applied: bounds={editor.Bounds}, lines={editor.Document.LineCount}, visible={editor.IsVisible}, content={first.ArticleContent.Length}, view={editor.TextArea.TextView.ScrollOffset}, viewers={string.Join(';', editor.GetVisualDescendants().OfType<ScrollViewer>().Select(v => v.Name + ':' + v.Offset.ToString()))}");
        var caret = editor.CaretOffset;
        await shell.Workspace.OpenPathAsync(two);
        await Task.Delay(200);
        shell.Workspace.ActiveDocument = first;
        await Task.Delay(300);
        editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        if (editor.CaretOffset != caret) throw new Exception($"Caret not restored: {editor.CaretOffset} != {caret}");
        if (!editor.Document.UndoStack.CanUndo) throw new Exception("Undo stack lost");
        if (editor.VerticalOffset < 100) throw new Exception($"Scroll position lost: {editor.VerticalOffset}, saved={first.VerticalOffset}, view={editor.TextArea.TextView.ScrollOffset}");
        shell.ActivePage = shell.AboutPage;
        await Task.Delay(100);
        shell.ActivePage = shell.Workspace;
        await Task.Delay(100);
        if (!ReferenceEquals(editor, window.GetVisualDescendants().OfType<TextEditor>().First())) throw new Exception("Workspace was recreated during navigation");
        Capture(window, Path.Combine(output, "workspace-light.png"));
    }

    public async Task SidebarAndMenus() {
        var workspaceView = window.GetVisualDescendants().OfType<ArticleWorkspaceView>().First();
        var headers = workspaceView.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Where(b => b.Classes.Contains("SectionHeader")).ToArray();
        if (headers.Length != 3 || headers.Any(h => h.Bounds.Height != 28 || h.Bounds.Width < 180)) throw new Exception("Sidebar headers are not compact full-width rows");
        shell.Workspace.SidebarColumnWidth = new GridLength(300);
        await Task.Delay(100);
        if (headers[0].Bounds.Width != 300) throw new Exception("Sidebar resize did not update layout");
        shell.Workspace.ToggleSidebarCommand.Execute(null);
        await Task.Delay(100);
        if (headers[0].IsEffectivelyVisible) throw new Exception("Sidebar did not hide");
        shell.Workspace.ToggleSidebarCommand.Execute(null);
        shell.Workspace.IsRecentExpanded = true;
        await Task.Delay(100);
        Capture(window, Path.Combine(output, "sidebar-expanded.png"));
        shell.Workspace.IsRecentExpanded = false;
        shell.Workspace.SidebarColumnWidth = new GridLength(240);
        mainMenu = window.FindControl<Menu>("MainMenu")!;
        if (mainMenu.Items.Count != 6) throw new Exception("Missing menu groups");
        ((MenuItem)mainMenu.Items[0]!).IsSubMenuOpen = true;
        await Task.Delay(150);
        ((MenuItem)mainMenu.Items[0]!).IsSubMenuOpen = false;
    }

    public async Task FocusAndPalette() {
        window.Activate();
        editor.TextArea.Focus();
        await Task.Delay(100);
        shell.RefreshCommands();
        if (!shell.GetCommand("edit.copy").Command.CanExecute(null)) throw new Exception("Editor command focus not captured");
        var summaryBox = window.GetVisualDescendants().OfType<TextBox>().First(box => box.Text == first.ArticleDescription);
        var originalSummary = summaryBox.Text;
        summaryBox.Focus();
        var replaceTask = shell.GetCommand("edit.replace").Command.ExecuteAsync(null);
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<FluentAvalonia.UI.Controls.FAContentDialog>().Any()), "Replace dialog did not open");
        var findDialog = window.GetVisualDescendants().OfType<FluentAvalonia.UI.Controls.FAContentDialog>().First();
        var fields = findDialog.GetVisualDescendants().OfType<TextBox>().ToArray();
        fields[0].Text = "团队"; fields[1].Text = "测试团队";
        ClickDialog(window, "全部替换");
        if (!summaryBox.Text!.Contains("测试团队")) throw new Exception("Replace did not target summary");
        ClickDialog(window, "关闭");
        await replaceTask;
        if (!summaryBox.CanUndo) throw new Exception("Summary replace is not undoable");
        await shell.GetCommand("edit.undo").Command.ExecuteAsync(null);
        if (summaryBox.Text != originalSummary) throw new Exception("Undo targeted wrong control");
        var paletteTask = shell.PaletteRequested!(true);
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<CommandPaletteView>().Any()), "Command palette did not open");
        var paletteView = window.GetVisualDescendants().OfType<CommandPaletteView>().First();
        var palette = (CommandPaletteViewModel)paletteView.DataContext!;
        palette.Query = "> 任务面板";
        await Task.Delay(250);
        if (palette.Entries.Count != 1) throw new Exception("Palette filtering failed");
        Capture(window, Path.Combine(output, "command-palette.png"));
        paletteView.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        await paletteTask;
        if (!shell.Workspace.IsTaskPanelOpen) throw new Exception("Palette did not execute command");
        await Task.Delay(200);
        Capture(window, Path.Combine(output, "task-panel.png"));
        shell.Workspace.IsTaskPanelOpen = false;
        first.NavigateToHeadingCommand.Execute(first.OutlineEntries[4]);
        await Task.Delay(100);
        if (editor.TextArea.Caret.Line != first.OutlineEntries[4].Line) throw new Exception("Outline navigation failed");
    }

    public async Task PreviewNavigation() {
        first.EditorMode = MarkdownEditorMode.Preview;
        await Task.Delay(600);
        var browser = window.GetVisualDescendants().OfType<NativeWebView>().First(b => b.Name == "PreviewBrowser");
        await WaitUntilAsync(async () => {
            try { return await browser.InvokeScript("!!document.querySelector('[data-outline-line]')") == "true"; }
            catch (InvalidOperationException) { return false; }
        }, "WebView2 page did not load; check installed runtime and desktop permissions", 15000);
        foreach (var mode in new[] { MarkdownEditorMode.Split, MarkdownEditorMode.Preview }) {
            first.EditorMode = MarkdownEditorMode.Source;
            await Task.Delay(150);
            editor.CaretOffset = 0;
            editor.ScrollToLine(85);
            await Task.Delay(150);
            var textView = editor.TextArea.TextView;
            var visibleLine = textView.VisualLines.First(v => v.VisualTop + v.Height > textView.VerticalOffset).FirstDocumentLine.LineNumber;
            if (visibleLine < 20) throw new Exception("Mode sync setup did not scroll source");
            first.EditorMode = mode;
            await Task.Delay(300);
            var synced = await browser.InvokeScript($$"""
                (() => {
                    const blocks = Array.from(document.querySelectorAll('[data-source-line]'));
                    const nearest = blocks.filter(e => Number(e.dataset.sourceLine) <= {{visibleLine}}).pop();
                    return window.scrollY > 100 && !!nearest && Math.abs(nearest.getBoundingClientRect().top) < 150;
                })()
                """);
            if (synced != "true" || editor.CaretOffset != 0 || first.EditorMode != mode)
                throw new Exception($"Mode switch failed to follow scrolled viewport: {mode}, line={visibleLine}, result={synced}");
        }
        var targetHeading = first.OutlineEntries[20];
        first.NavigateToHeadingCommand.Execute(targetHeading);
        await Task.Delay(250);
        var position = await browser.InvokeScript($"Math.abs(document.querySelector('[data-outline-line=\"{targetHeading.Line}\"]').getBoundingClientRect().top) < 2");
        if (first.EditorMode != MarkdownEditorMode.Preview || position != "true") throw new Exception("Preview outline did not scroll in preview mode: " + position);
        await browser.InvokeScript("window.scrollTo(0, 0)");
        first.NavigateToHeadingCommand.Execute(targetHeading);
        await Task.Delay(150);
        position = await browser.InvokeScript($"Math.abs(document.querySelector('[data-outline-line=\"{targetHeading.Line}\"]').getBoundingClientRect().top) < 2");
        if (position != "true") throw new Exception("Repeated outline navigation failed: " + position);
        first.EditorMode = MarkdownEditorMode.Split;
        first.NavigateToHeadingCommand.Execute(first.OutlineEntries[10]);
        await Task.Delay(150);
        if (first.EditorMode != MarkdownEditorMode.Split || editor.TextArea.Caret.Line != first.OutlineEntries[10].Line) throw new Exception("Split outline navigation changed mode or failed");
        first.EditorMode = MarkdownEditorMode.Source;
    }

    public async Task ThemeAndClose() {
        shell.ApplyTheme(true);
        await Task.Delay(300);
        Capture(window, Path.Combine(output, "workspace-dark.png"));
        shell.ApplyTheme(false);
        window.Width = 800;
        window.Height = 650;
        await Task.Delay(300);
        Capture(window, Path.Combine(output, "workspace-narrow.png"));
        if (mainMenu.IsVisible || !window.FindControl<Menu>("CompactMenu")!.IsVisible) throw new Exception("Menu did not collapse at 800px");
        var canceled = shell.Workspace.CloseActiveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "取消" && b.IsEffectivelyVisible)), "Close confirmation did not open");
        ClickDialog(window, "取消");
        await canceled;
        if (shell.Workspace.Documents.Count != 2 || !first.IsDocumentDirty) throw new Exception("Cancel lost document state");
        var discarded = shell.Workspace.CloseActiveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "不保存" && b.IsEffectivelyVisible)), "Close confirmation did not open");
        ClickDialog(window, "不保存");
        await discarded;
        if (shell.Workspace.Documents.Count != 1) throw new Exception("Discard did not close document");
        shell.PublishPage.ArticleContent += "\n\n保存测试";
        var saved = shell.Workspace.CloseActiveCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => Task.FromResult(window.GetVisualDescendants().OfType<Button>().Any(b => b.Content as string == "保存" && b.IsEffectivelyVisible)), "Close confirmation did not open");
        ClickDialog(window, "保存");
        await saved;
        if (shell.Workspace.HasDocuments) throw new Exception("Save-and-close did not return to welcome");
        if (!(await File.ReadAllTextAsync(two)).Contains("保存测试")) throw new Exception("Save-and-close lost content");
        await shell.Workspace.OpenPathAsync(two);
        shell.PublishPage.IsPreviewMode = true;
        await Task.Delay(300);
        shell.PublishPage.IsSplitMode = true;
        await Task.Delay(200);
        shell.PublishPage.IsSourceMode = true;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string message, int timeoutMs = 5000) {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < timeoutMs) {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException(message);
    }

    private static void ClickDialog(Window window, string label) {
        var dialog = window.GetVisualDescendants().OfType<FluentAvalonia.UI.Controls.FAContentDialog>().First();
        var button = dialog.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == label);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static void Capture(Window window, string path) {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }
}
