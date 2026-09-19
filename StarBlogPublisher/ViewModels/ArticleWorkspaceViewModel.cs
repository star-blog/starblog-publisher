using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
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
    public ObservableCollection<PublishViewModel> Documents { get; } = new();
    public ObservableCollection<RecentArticle> RecentFiles { get; } = new();
    public PublishViewModel EmptyDocument { get; }
    [ObservableProperty] private PublishViewModel? _activeDocument;
    [ObservableProperty] private bool _isSidebarOpen = true;
    public bool HasDocuments => Documents.Count > 0;
    public bool HasPreviousSession => _previousSession.OpenFiles?.Length > 0;
    public PublishViewModel CurrentDocument => ActiveDocument ?? EmptyDocument;

    public ArticleWorkspaceViewModel(MainWindowViewModel shell, string? historyPath = null) : base("文章", Icon.Pen) {
        _shell = shell;
        _historyStore = new WorkspaceHistoryStore(historyPath ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(AppSettings.SettingsFilePath))!, "workspace.json"));
        _previousSession = _historyStore.Load();
        foreach (var path in (_previousSession.RecentFiles ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Take(12)) RecentFiles.Add(new(path));
        EmptyDocument = new PublishViewModel(shell);
        Documents.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDocuments));
    }

    partial void OnActiveDocumentChanged(PublishViewModel? value) {
        OnPropertyChanged(nameof(CurrentDocument));
        _shell.RefreshChromeTitle();
        SaveHistory();
    }

    [RelayCommand] private void ToggleSidebar() => IsSidebarOpen = !IsSidebarOpen;
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

    private void SaveHistory() => _historyStore.Save(new(RecentFiles.Select(item => item.Path).ToArray(),
        Documents.Select(d => d.CurrentFilePath).OfType<string>().ToArray(), ActiveDocument?.CurrentFilePath));

    [RelayCommand] private async Task RestoreSession() {
        foreach (var path in _previousSession.OpenFiles ?? []) {
            if (File.Exists(path)) await OpenPathAsync(path);
        }
        var selected = Documents.FirstOrDefault(d => d.CurrentFilePath == _previousSession.ActiveFile);
        if (selected != null) ActiveDocument = selected;
    }

    [RelayCommand] private void NewDocument() {
        var document = new PublishViewModel(_shell);
        document.InitializeNewDocument();
        document.NotifyLoginState(_shell.IsLoggedIn);
        document.Categories = new(CurrentDocument.Categories);
        Documents.Add(document);
        ActiveDocument = document;
    }

    [RelayCommand] private Task SaveActive() => CurrentDocument.SaveDocumentAsync();
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
