using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel {
    private double _inspectorWidth = 300;
    public double ExpandedInspectorWidth => _inspectorWidth;
    public Avalonia.Controls.GridLength InspectorColumnWidth {
        get => new(IsInspectorOpen ? _inspectorWidth : 0);
        set {
            if (IsInspectorOpen && value.IsAbsolute) _inspectorWidth = Math.Clamp(value.Value, 240, 480);
            OnPropertyChanged();
        }
    }
    private string _savedMetadata = "";
    private string? _sidecarPostId;
    private bool? _sidecarIsPublish;
    private DateTime? _sidecarLastSyncedAt;
    private bool _isNewDocument;
    private bool _isSaving;
    private bool _isCheckingClose;
    private readonly string _previewId = Guid.NewGuid().ToString("N");
    public AvaloniaEdit.Document.TextDocument EditorDocument { get; } = new();
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public string OutlineText => string.Join(Environment.NewLine, ArticleContent.Split('\n')
        .Where(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"^#{1,6}\s+"))
        .Select(line => line.TrimEnd('\r')));
    private string MetadataSnapshot() => JsonSerializer.Serialize(new ArticleProperties(
        ArticleTitle, ArticleDescription, ArticleSlug, ArticleKeywords, SelectedCategory?.Id));

    public void InitializeNewDocument() {
        HasLoadedArticle = true;
        ArticleTitle = "未命名文章";
        _isNewDocument = true;
        ClearSidecarIdentity();
        _savedMetadata = MetadataSnapshot();
        NotifyDocumentState();
    }

    private async Task LoadPropertiesAsync() {
        ClearSidecarIdentity();
        try {
            if (!string.IsNullOrEmpty(_currentFilePath)) {
                var sidecar = await ArticleSidecar.ReadAsync(_currentFilePath);
                if (sidecar != null) {
                    ArticleTitle = sidecar.Title;
                    ArticleDescription = sidecar.Description;
                    ArticleSlug = sidecar.Slug;
                    ArticleKeywords = sidecar.Keywords;
                    SelectedCategory = sidecar.Category;
                    _sidecarPostId = sidecar.PostId;
                    _sidecarIsPublish = sidecar.IsPublish;
                    _sidecarLastSyncedAt = sidecar.LastSyncedAt;
                }
            }
        }
        catch (Exception ex) { GuiHost.ToastWarning("文章属性未加载", ex.Message); }
        _savedMetadata = MetadataSnapshot();
        NotifyDocumentState();
    }

    [RelayCommand] private Task SaveDocument() => SaveDocumentAsync();
    public async Task<bool> SaveDocumentAsync(bool saveAs = false) {
        if (!HasLoadedArticle || IsWorkspaceBusy || _isSaving) return false;
        _isSaving = true;
        try {
            var path = _currentFilePath;
            if (saveAs || string.IsNullOrEmpty(path)) {
                var storage = GuiHost.GetTopLevel()?.StorageProvider;
                if (storage == null) return false;
                var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
                    Title = "保存 Markdown 文章", SuggestedFileName = DocumentFileName, DefaultExtension = "md",
                    FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }]
                });
                path = file?.TryGetLocalPath();
                if (string.IsNullOrEmpty(path)) return false;
                if (_shell.Workspace.Documents.Any(d => d != this && SameDocumentPath(d.CurrentFilePath, path))) {
                    GuiHost.ToastWarning("无法保存", "该文件已在另一个标签中打开，请选择其他文件名。");
                    return false;
                }
            }
            var content = ArticleContent;
            var keepIdentity = SameDocumentPath(path, _currentFilePath);
            var sidecar = new ArticleSidecar {
                Title = ArticleTitle,
                Description = ArticleDescription,
                Slug = ArticleSlug,
                Keywords = ArticleKeywords,
                Category = SelectedCategory,
                PostId = keepIdentity ? _sidecarPostId : null,
                IsPublish = keepIdentity ? _sidecarIsPublish : null,
                LastSyncedAt = keepIdentity ? _sidecarLastSyncedAt : null
            };
            await WriteFileAtomicallyAsync(path, content);
            await ArticleSidecar.WriteAsync(path, sidecar);
            _currentFilePath = path;
            _loadedContent = content;
            _sidecarPostId = sidecar.PostId;
            _sidecarIsPublish = sidecar.IsPublish;
            _sidecarLastSyncedAt = sidecar.LastSyncedAt;
            _savedMetadata = MetadataSnapshot();
            _isNewDocument = false;
            _shell.Workspace.RememberFile(path);
            NotifyDocumentState();
            RefreshPreview();
            StatusMessage = "正文与文章属性已保存";
            return true;
        }
        catch (Exception ex) { GuiHost.ToastError("保存失败", ex.Message); return false; }
        finally { _isSaving = false; }
    }

    private static async Task WriteFileAtomicallyAsync(string path, string content) {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
    }

    public async Task<bool> CanCloseAsync() {
        if (_isCheckingClose) return false;
        _isCheckingClose = true;
        try { return await ConfirmCloseAsync(); }
        finally { _isCheckingClose = false; }
    }

    private async Task<bool> ConfirmCloseAsync() {
        if (_isSaving || IsWorkspaceBusy || IsPreparingAi || IsRefiningTitle || RegenerateDescriptionCommand.IsRunning
            || GenerateKeywordsCommand.IsRunning || GenerateSlugCommand.IsRunning || SaveDocumentCommand.IsRunning) {
            GuiHost.ToastWarning("暂时无法关闭", "这篇文章仍有任务正在执行，请完成后再关闭。");
            return false;
        }
        if (!IsDocumentDirty) return true;
        var dialog = new FAContentDialog {
            Title = $"保存对 {DocumentFileName} 的修改？",
            Content = "正文保存到 Markdown 文件，文章属性保存到同目录的 .starblog.json 文件。",
            PrimaryButtonText = "保存", SecondaryButtonText = "不保存", CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync(GuiHost.GetMainWindow());
        return result == FAContentDialogResult.Secondary || result == FAContentDialogResult.Primary && await SaveDocumentAsync();
    }

    partial void OnArticleDescriptionChanged(string value) => NotifyDocumentState();
    partial void OnArticleSlugChanged(string value) => NotifyDocumentState();

    private void ClearSidecarIdentity() {
        _sidecarPostId = null;
        _sidecarIsPublish = null;
        _sidecarLastSyncedAt = null;
    }

    private static bool SameDocumentPath(string? left, string? right) =>
        !string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right) &&
        string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private record ArticleProperties(string Title, string Description, string Slug, string Keywords, int? CategoryId);
}
