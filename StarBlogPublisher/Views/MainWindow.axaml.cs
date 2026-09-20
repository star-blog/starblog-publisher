using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Data;
using Avalonia.Layout;
using AvaloniaEdit;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Views.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class MainWindow : FAAppWindow {
    private bool _allowClose;
    private bool _checkingClose;
    private Control? _editTarget;
    private bool _paletteOpen;
    public MainWindow() {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        Opened += (_, _) => {
            GuiHost.SetFeedbackBar(FeedbackBar);
            SyncTitleBarMetrics();
        };
        LayoutUpdated += (_, _) => SyncTitleBarMetrics();
        Closing += OnClosing;
        AddHandler(KeyDownEvent, OnWorkspaceKeyDown, RoutingStrategies.Tunnel);
        AddHandler(GotFocusEvent, OnEditorGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        DataContextChanged += (_, _) => InitializeShellMenus();
    }

    private void InitializeShellMenus() {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.CanEdit = CanEditTarget;
        vm.EditRequested = ExecuteEdit;
        vm.PaletteRequested = ShowPaletteAsync;
        vm.ExitRequested = Close;
        MainMenu.Items.Clear(); CompactMenu.Items.Clear();
        var compactRoot = new MenuItem { Header = "☰", Classes = { "ChromeMenuItem" } };
        foreach (var group in vm.ShellCommands.GroupBy(command => command.Group)) {
            MainMenu.Items.Add(CreateMenuGroup(group.Key, group));
            compactRoot.Items.Add(CreateMenuGroup(group.Key, group));
        }
        CompactMenu.Items.Add(compactRoot);
        var recentMenus = new[] { (MenuItem)MainMenu.Items[0]!, (MenuItem)compactRoot.Items[0]! }
            .Select(group => { var recent = new MenuItem { Header = "最近打开" }; group.Items.Insert(3, recent); return recent; }).ToArray();
        void RefreshRecent() {
            foreach (var menu in recentMenus) {
                menu.Items.Clear();
                foreach (var recent in vm.Workspace.RecentFiles) {
                    var item = new MenuItem { Header = recent.FileName, Command = new AsyncRelayCommand(async () => {
                        vm.ActivePage = vm.Workspace; await vm.Workspace.OpenPathAsync(recent.Path);
                    }) };
                    ToolTip.SetTip(item, recent.Path);
                    menu.Items.Add(item);
                }
                menu.IsEnabled = menu.Items.Count > 0;
            }
        }
        vm.Workspace.RecentFiles.CollectionChanged += (_, _) => RefreshRecent();
        RefreshRecent();
        MainMenu.PointerEntered += (_, _) => vm.RefreshCommands();
        CompactMenu.PointerEntered += (_, _) => vm.RefreshCommands();
    }

    private static MenuItem CreateMenuGroup(string name, System.Collections.Generic.IEnumerable<ShellCommand> commands) {
        var accessKey = name switch { "文件" => "F", "编辑" => "E", "查看" => "V", "文章" => "A", "发布" => "P", _ => "H" };
        var group = new MenuItem { Header = $"{name}(_{accessKey})", Classes = { "ChromeMenuItem" } };
        foreach (var definition in commands) {
            var item = new MenuItem { Header = definition.Label, Command = definition.Command };
            if (definition.Shortcut != null) item.InputGesture = KeyGesture.Parse(definition.Shortcut);
            if (definition.IsToggle) {
                item.ToggleType = MenuItemToggleType.CheckBox;
                item.Bind(MenuItem.IsCheckedProperty, new Binding(nameof(ShellCommand.IsChecked)) { Source = definition, Mode = BindingMode.OneWay });
            }
            group.Items.Add(item);
        }
        return group;
    }

    private async void OnQuickOpen(object? sender, RoutedEventArgs e) => await ShowPaletteAsync(false);

    private async Task ShowPaletteAsync(bool commands) {
        if (_paletteOpen || DataContext is not MainWindowViewModel vm) return;
        _paletteOpen = true;
        PaletteEntry? selected = null;
        try {
            var model = new CommandPaletteViewModel(vm, commands);
            var view = new CommandPaletteView { DataContext = model };
            var dialog = new FAContentDialog { Title = commands ? "命令面板" : "快速打开", Content = view, CloseButtonText = "取消" };
            view.ExecuteRequested += () => {
                if (model.SelectedEntry is not { } entry || !entry.CanExecute()) return;
                selected = entry;
                dialog.Hide();
            };
            await dialog.ShowAsync(this);
        }
        finally { _paletteOpen = false; }
        if (selected?.CanExecute() == true) await selected.Execute();
    }

    private void OnEditorGotFocus(object? sender, RoutedEventArgs e) {
        if (e.Source is not Control control || control.GetVisualAncestors().Any(v => v is FAContentDialog)) return;
        if (control is TextBox textBox) _editTarget = textBox;
        else if (control is TextEditor focusedEditor) _editTarget = focusedEditor;
        else if (control.GetVisualAncestors().OfType<TextEditor>().FirstOrDefault() is { } editor) _editTarget = editor;
        if (DataContext is MainWindowViewModel vm) vm.RefreshCommands();
    }

    private bool CanEditTarget(string action) {
        if (_editTarget == null || TopLevel.GetTopLevel(_editTarget) != this || !_editTarget.IsEffectivelyVisible) return false;
        if (_editTarget.GetVisualAncestors().OfType<PublishEditorView>().FirstOrDefault()?.DataContext is PublishViewModel { IsWorkspaceBusy: true }) return false;
        return _editTarget switch {
            TextBox box => action switch { "undo" => box.CanUndo, "redo" => box.CanRedo,
                "cut" or "replace" or "paste" => !box.IsReadOnly, _ => true },
            TextEditor editor => action switch { "undo" => editor.CanUndo, "redo" => editor.CanRedo,
                "cut" or "replace" or "paste" => !editor.IsReadOnly, _ => true },
            _ => false
        };
    }

    private async Task ExecuteEdit(string action) {
        if (!CanEditTarget(action)) return;
        var target = _editTarget!;
        if (action is "find" or "replace") { await ShowFindAsync(target, action == "replace"); return; }
        target.Focus();
        if (target is TextBox box) {
            switch (action) { case "undo": box.Undo(); break; case "redo": box.Redo(); break;
                case "cut": box.Cut(); break; case "copy": box.Copy(); break; case "paste": box.Paste(); break; case "selectAll": box.SelectAll(); break; }
        }
        else if (target is TextEditor editor) {
            switch (action) { case "undo": editor.Undo(); break; case "redo": editor.Redo(); break;
                case "cut": editor.Cut(); break; case "copy": editor.Copy(); break; case "paste": editor.Paste(); break; case "selectAll": editor.SelectAll(); break; }
        }
    }

    private async Task ShowFindAsync(Control target, bool replace) {
        var query = new TextBox { PlaceholderText = "查找内容" };
        var replacement = new TextBox { PlaceholderText = "替换为", IsVisible = replace };
        var message = new TextBlock { Text = "在当前编辑控件中查找（区分大小写）", FontSize = 12 };
        var panel = new StackPanel { Spacing = 10, MinWidth = 360, Children = { query, replacement, message } };
        var dialog = new FAContentDialog { Title = replace ? "查找与替换" : "查找", Content = panel,
            PrimaryButtonText = "查找下一个", SecondaryButtonText = replace ? "全部替换" : null, CloseButtonText = "关闭" };
        int offset = 0;
        string Content() => target is TextBox box ? box.Text ?? "" : ((TextEditor)target).Text;
        dialog.PrimaryButtonClick += (_, e) => {
            e.Cancel = true;
            if (string.IsNullOrEmpty(query.Text)) return;
            var content = Content();
            var found = content.IndexOf(query.Text, Math.Min(offset, content.Length), StringComparison.Ordinal);
            if (found < 0) found = content.IndexOf(query.Text, StringComparison.Ordinal);
            message.Text = found < 0 ? "未找到匹配内容" : $"找到匹配项，位置 {found + 1}";
            if (found < 0) return;
            offset = found + query.Text.Length;
            if (target is TextBox box) { box.SelectionStart = found; box.SelectionEnd = offset; }
            else { var editor = (TextEditor)target; editor.Select(found, query.Text.Length); editor.ScrollToLine(editor.Document.GetLineByOffset(found).LineNumber); }
        };
        dialog.SecondaryButtonClick += (_, e) => {
            e.Cancel = true;
            if (string.IsNullOrEmpty(query.Text)) return;
            var content = Content();
            var updated = content.Replace(query.Text, replacement.Text ?? "", StringComparison.Ordinal);
            if (target is TextBox box) { box.SelectAll(); box.SelectedText = updated; }
            else { var editor = (TextEditor)target; editor.Document.Replace(0, editor.Document.TextLength, updated); }
            message.Text = content == updated ? "没有需要替换的内容" : "已替换，可撤销";
            offset = 0;
        };
        await dialog.ShowAsync(this);
        target.Focus();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e) {
        if (_allowClose || DataContext is not MainWindowViewModel vm) return;
        e.Cancel = true;
        if (_checkingClose) return;
        _checkingClose = true;
        try {
            if (await vm.Workspace.CanCloseAllAsync()) { _allowClose = true; Close(); }
        }
        finally { _checkingClose = false; }
    }

    private void OnWorkspaceKeyDown(object? sender, KeyEventArgs e) {
        if (e.Source is Visual source && source.GetVisualAncestors().Any(ancestor => ancestor is FAContentDialog)) return;
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Key is Key.LeftAlt or Key.RightAlt) vm.RefreshCommands();
        foreach (var command in vm.ShellCommands.Where(c => c.Shortcut != null)) {
            if (command.Group == "编辑" && command.Id is not ("edit.find" or "edit.replace")) continue;
            if (!KeyGesture.Parse(command.Shortcut!).Matches(e)) continue;
            if (command.Command.CanExecute(null)) command.Command.Execute(null);
            e.Handled = true; return;
        }
    }

    private void SyncTitleBarMetrics() {
        if (DataContext is MainWindowViewModel vm) {
            vm.UpdateTitleBarMetrics(Math.Max(36, TitleBar.Height), 0);
            MainMenu.IsVisible = Bounds.Width >= 1080;
            CompactMenu.IsVisible = !MainMenu.IsVisible;
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.Source != sender) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else BeginMoveDrag(e);
        }
    }
}
