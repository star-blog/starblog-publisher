using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class ArticleWorkspaceViewModel : PageViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly WorkspaceHistoryStore _historyStore;
    private readonly WorkspaceHistory _previousSession;
    private readonly SemaphoreSlim _openGate = new(1, 1);
    private WorkspaceLayout _layout = new();
    public ObservableCollection<PublishViewModel> Documents { get; } = new();
    public ObservableCollection<RecentArticle> RecentFiles { get; } = new();
    public ObservableCollection<ArticleWorkspaceTreeNode> WorkspaceFiles { get; } = new();
    public PublishViewModel EmptyDocument { get; }
    [ObservableProperty] private PublishViewModel? _activeDocument;
    [ObservableProperty] private bool _isSidebarOpen = true;
    [ObservableProperty] private bool _isTaskPanelOpen;
    [ObservableProperty] private bool _isFocusMode;
    [ObservableProperty] private bool _isOpenDocumentsExpanded = true;
    [ObservableProperty] private bool _isRecentFilesExpanded;
    [ObservableProperty] private bool _isOutlineExpanded = true;
    [ObservableProperty] private bool _isWorkspaceTreeExpanded = true;
    [ObservableProperty] private string _workspaceRootName = "文章工作区";
    [ObservableProperty] private string? _workspaceRootPath;
    private double _sidebarWidth = 240;
    private bool _sidebarBeforeFocus;
    private bool _tasksBeforeFocus;
    private readonly Dictionary<PublishViewModel, bool> _inspectorsBeforeFocus = new();
    public bool HasDocuments => Documents.Count > 0;
    public bool HasPreviousSession => _previousSession.OpenFiles?.Length > 0;
    public PublishViewModel CurrentDocument => ActiveDocument ?? EmptyDocument;

    public ArticleWorkspaceViewModel(MainWindowViewModel shell, string? historyPath = null) : base("文章", Icon.Pen) {
        _shell = shell;
        _historyStore = new WorkspaceHistoryStore(historyPath ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(AppSettings.SettingsFilePath))!, "workspace.json"));
        _previousSession = _historyStore.Load();
        _layout = _previousSession.Layout ?? new();
        _isSidebarOpen = _layout.SidebarOpen;
        _isTaskPanelOpen = _layout.TasksOpen;
        _sidebarWidth = Math.Clamp(_layout.SidebarWidth, 180, 420);
        foreach (var path in (_previousSession.RecentFiles ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(12)) RecentFiles.Add(new(path));
        EmptyDocument = new PublishViewModel(shell);
        Documents.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDocuments));
    }

    partial void OnActiveDocumentChanged(PublishViewModel? value) {
        if (value != null) value.PropertyChanged += OnDocumentLayoutChanged;
        if (IsFocusMode && value != null) HideInspectorForFocus(value);
        OnPropertyChanged(nameof(CurrentDocument));
        RefreshWorkspaceFiles();
        _shell.RefreshChromeTitle();
        SaveHistory();
    }

    partial void OnActiveDocumentChanging(PublishViewModel? value) {
        if (ActiveDocument != null) ActiveDocument.PropertyChanged -= OnDocumentLayoutChanged;
    }
    public Avalonia.Controls.GridLength SidebarColumnWidth {
        get => new(IsSidebarOpen ? _sidebarWidth : 0);
        set {
            if (!value.IsAbsolute || !IsSidebarOpen) return;
            var width = Math.Clamp(value.Value, 180, 420);
            if (Math.Abs(width - _sidebarWidth) < 0.1) return;
            _sidebarWidth = width;
            OnPropertyChanged();
            SaveHistory();
        }
    }
    public Avalonia.Controls.GridLength SidebarSplitterColumnWidth => new(IsSidebarOpen ? 4 : 0);

    partial void OnIsSidebarOpenChanged(bool value) {
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarSplitterColumnWidth));
        if (!IsFocusMode) SaveHistory();
    }
    partial void OnIsTaskPanelOpenChanged(bool value) { if (!IsFocusMode) SaveHistory(); }
    private void OnDocumentLayoutChanged(object? sender, PropertyChangedEventArgs e) {
        if (IsFocusMode || sender is not PublishViewModel document) return;
        if (e.PropertyName is nameof(PublishViewModel.IsInspectorOpen) or nameof(PublishViewModel.InspectorColumnWidth)) {
            _layout = _layout with { InspectorOpen = document.IsInspectorOpen, InspectorWidth = document.ExpandedInspectorWidth };
            SaveHistory();
        }
    }
    private void ApplyLayout(PublishViewModel document) {
        document.InspectorColumnWidth = new Avalonia.Controls.GridLength(Math.Clamp(_layout.InspectorWidth, 240, 480));
        document.IsInspectorOpen = _layout.InspectorOpen;
    }

    private void RefreshWorkspaceFiles() {
        WorkspaceFiles.Clear();
        var filePath = ActiveDocument?.CurrentFilePath;
        if (string.IsNullOrWhiteSpace(filePath)) {
            WorkspaceRootName = "文章工作区";
            WorkspaceRootPath = null;
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;
        WorkspaceRootPath = directory;
        WorkspaceRootName = new DirectoryInfo(directory).Name;
        foreach (var entry in ReadDirectory(directory, 0)) WorkspaceFiles.Add(entry);
    }

    private static IEnumerable<ArticleWorkspaceTreeNode> ReadDirectory(string directory, int depth) {
        if (depth > 4) yield break;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(directory).OrderBy(path => !Directory.Exists(path)).ThenBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase).ToArray(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }

        foreach (var path in entries) {
            if (Directory.Exists(path)) {
                var children = ReadDirectory(path, depth + 1).ToArray();
                if (children.Length > 0) yield return new(path, true, children);
            }
            else if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)) {
                yield return new(path, false, []);
            }
        }
    }

    [RelayCommand] private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;
    [RelayCommand] private void ToggleTasks() => IsTaskPanelOpen = !IsTaskPanelOpen;
    [RelayCommand] private void ToggleFocus() {
        if (!IsFocusMode) {
            _sidebarBeforeFocus = IsSidebarOpen;
            _tasksBeforeFocus = IsTaskPanelOpen;
            IsFocusMode = true;
            IsSidebarOpen = false; IsTaskPanelOpen = false;
            if (ActiveDocument != null) HideInspectorForFocus(ActiveDocument);
        }
        else {
            IsFocusMode = false;
            IsSidebarOpen = _sidebarBeforeFocus; IsTaskPanelOpen = _tasksBeforeFocus;
            foreach (var item in _inspectorsBeforeFocus) item.Key.IsInspectorOpen = item.Value;
            _inspectorsBeforeFocus.Clear();
        }
    }
    private void HideInspectorForFocus(PublishViewModel document) {
        _inspectorsBeforeFocus.TryAdd(document, document.IsInspectorOpen);
        document.IsInspectorOpen = false;
    }
    [RelayCommand] private void Activate(PublishViewModel document) => ActiveDocument = document;
    [RelayCommand] private void NextDocument() {
        if (Documents.Count > 0) ActiveDocument = Documents[(Documents.IndexOf(CurrentDocument) + 1) % Documents.Count];
    }

    [RelayCommand] private async Task OpenFiles() {
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "打开 Markdown 文章", AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md", "*.markdown"] }]
        });
        foreach (var file in files) {
            var path = file.TryGetLocalPath();
            if (path != null) await OpenPathAsync(path);
        }
    }

    [RelayCommand] private Task OpenRecent(string path) => OpenPathAsync(path);

    public async Task OpenPathAsync(string path) {
        await _openGate.WaitAsync();
        try {
            path = Path.GetFullPath(path);
            var existing = Documents.FirstOrDefault(d => string.Equals(d.CurrentFilePath, path,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (existing != null) { ActiveDocument = existing; return; }
            var document = new PublishViewModel(_shell);
            ApplyLayout(document);
            document.NotifyLoginState(_shell.IsLoggedIn);
            document.Categories = new(CurrentDocument.Categories);
            await document.LoadFromPathAsync(path);
            if (!document.HasLoadedArticle) return;
            Documents.Add(document);
            ActiveDocument = document;
            RememberFile(path);
        }
        catch (Exception ex) { GuiHost.ToastError("打开失败", ex.Message); }
        finally { _openGate.Release(); }
    }

    public void RememberFile(string path) {
        var existing = RecentFiles.FirstOrDefault(item => string.Equals(item.Path, path,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        if (existing != null) RecentFiles.Remove(existing);
        RecentFiles.Insert(0, new(path));
        while (RecentFiles.Count > 12) RecentFiles.RemoveAt(RecentFiles.Count - 1);
        SaveHistory();
    }

    private void SaveHistory() {
        if (!IsFocusMode) _layout = _layout with {
            SidebarOpen = IsSidebarOpen,
            TasksOpen = IsTaskPanelOpen,
            SidebarWidth = _sidebarWidth
        };
        _historyStore.Save(new(RecentFiles.Select(item => item.Path).ToArray(),
            Documents.Select(d => d.CurrentFilePath).OfType<string>().ToArray(), ActiveDocument?.CurrentFilePath, _layout));
    }

    [RelayCommand] private async Task RestoreSession() {
        foreach (var path in _previousSession.OpenFiles ?? []) {
            if (File.Exists(path)) await OpenPathAsync(path);
        }
        var selected = Documents.FirstOrDefault(d => d.CurrentFilePath == _previousSession.ActiveFile);
        if (selected != null) ActiveDocument = selected;
    }

    [RelayCommand] private void NewDocument() {
        var document = new PublishViewModel(_shell);
        ApplyLayout(document);
        document.InitializeNewDocument();
        document.NotifyLoginState(_shell.IsLoggedIn);
        document.Categories = new(CurrentDocument.Categories);
        Documents.Add(document);
        ActiveDocument = document;
    }

    [RelayCommand] private Task SaveActive() => CurrentDocument.SaveDocumentAsync();
    [RelayCommand] private async Task SaveAll() {
        foreach (var document in Documents.ToArray()) {
            if (document.IsDocumentDirty && !await document.SaveDocumentAsync()) break;
        }
    }
    [RelayCommand] private Task CloseAll() => CloseRangeAsync(Documents.ToArray());
    [RelayCommand] private Task CloseOthers(PublishViewModel document) => CloseRangeAsync(Documents.Where(d => d != document).ToArray());
    [RelayCommand] private Task CloseRight(PublishViewModel document) => CloseRangeAsync(Documents.Skip(Documents.IndexOf(document) + 1).ToArray());
    private async Task CloseRangeAsync(PublishViewModel[] documents) {
        foreach (var document in documents) {
            await CloseDocument(document);
            if (Documents.Contains(document)) break;
        }
    }
    [RelayCommand] private Task CloseActive() => CloseDocument(ActiveDocument);
    [RelayCommand] private async Task CloseDocument(PublishViewModel? document) {
        if (document == null || !Documents.Contains(document) || !await document.CanCloseAsync()) return;
        var index = Documents.IndexOf(document);
        var previouslyActive = ActiveDocument;
        Documents.Remove(document);
        ActiveDocument = previouslyActive == document
            ? Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)]
            : previouslyActive;
        SaveHistory();
    }

    public async Task<bool> CanCloseAllAsync() {
        foreach (var document in Documents.ToArray()) {
            if (!await document.CanCloseAsync()) return false;
        }
        return true;
    }
}

public sealed record RecentArticle(string Path) {
    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed class ArticleWorkspaceTreeNode {
    public ArticleWorkspaceTreeNode(string path, bool isDirectory, IEnumerable<ArticleWorkspaceTreeNode> children) {
        Path = path;
        IsDirectory = isDirectory;
        Name = System.IO.Path.GetFileName(path);
        Children = new(children);
    }
    public string Path { get; }
    public string Name { get; }
    public bool IsDirectory { get; }
    public ObservableCollection<ArticleWorkspaceTreeNode> Children { get; }
}
