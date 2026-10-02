using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class SitePostEditorViewModel : ViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly ArticleLibraryApplicationService _library;
    private readonly CategoryApplicationService _categories;
    private readonly Action<bool> _onClosed;
    private string _loadedSnapshot = string.Empty;
    private bool _changed;

    public SitePostEditorViewModel(
        MainWindowViewModel shell,
        ArticleLibraryApplicationService library,
        CategoryApplicationService categories,
        string postId,
        Action<bool> onClosed) {
        _shell = shell;
        _library = library;
        _categories = categories;
        _onClosed = onClosed;
        PostId = postId;
    }

    public string PostId { get; }
    public ObservableCollection<Category> Categories { get; } = new();

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _slug = string.Empty;
    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private bool _isPublish;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "正在加载文章…";
    [ObservableProperty] private string? _postUrl;
    [ObservableProperty] private DateTime _lastUpdateTime;

    public string StatusText => IsPublish ? "已发布" : "草稿";
    public bool IsDirty => Snapshot() != _loadedSnapshot;
    public bool CanSave => !IsBusy && !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Content) && SelectedCategory != null;

    partial void OnTitleChanged(string value) => NotifyEditorState();
    partial void OnSummaryChanged(string value) => NotifyEditorState();
    partial void OnSlugChanged(string value) => NotifyEditorState();
    partial void OnContentChanged(string value) => NotifyEditorState();
    partial void OnSelectedCategoryChanged(Category? value) => NotifyEditorState();
    partial void OnIsPublishChanged(bool value) {
        OnPropertyChanged(nameof(StatusText));
        NotifyEditorState();
    }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    public async Task LoadAsync() {
        IsBusy = true;
        StatusMessage = "正在加载文章…";
        try {
            await LoadCategoriesAsync();
            var result = await _library.GetAsync(PostId);
            if (!result.Success || result.Post == null) {
                StatusMessage = result.ErrorMessage ?? "文章不存在";
                return;
            }

            ApplyPost(result.Post, result.PostUrl);
            StatusMessage = "已加载线上文章";
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task SaveDraft() => SaveAsync(publish: false);

    [RelayCommand]
    private Task Publish() => SaveAsync(publish: true);

    [RelayCommand]
    private async Task Delete() {
        if (!await GuiHost.ConfirmAsync("删除文章", $"确定删除「{Title}」？此操作不能撤销。")) return;
        IsBusy = true;
        try {
            var result = await _library.DeleteAsync(PostId);
            if (!result.Success) {
                GuiHost.ToastError("删除失败", result.ErrorMessage ?? "删除失败");
                StatusMessage = result.ErrorMessage ?? "删除失败";
                return;
            }

            _changed = true;
            GuiHost.ToastSuccess("已删除", Title);
            _onClosed(true);
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Download() {
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "保存为 Markdown",
            SuggestedFileName = SuggestFileName(),
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        try {
            var result = await _library.PullAsync(PostId, path);
            if (!result.Success) {
                GuiHost.ToastError("下载失败", result.ErrorMessage ?? "下载失败");
                return;
            }

            GuiHost.ToastSuccess("已下载", path);
            StatusMessage = $"已保存到 {path}";
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenInWorkspace() {
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "保存后在工作区打开",
            SuggestedFileName = SuggestFileName(),
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        IsBusy = true;
        try {
            var result = await _library.PullAsync(PostId, path);
            if (!result.Success) {
                GuiHost.ToastError("下载失败", result.ErrorMessage ?? "下载失败");
                return;
            }

            _shell.ActivePage = _shell.Workspace;
            await _shell.Workspace.OpenPathAsync(path);
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenInBrowser() {
        if (string.IsNullOrWhiteSpace(PostUrl)) return;
        Process.Start(new ProcessStartInfo(PostUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void Close() => _onClosed(_changed);

    private async Task SaveAsync(bool publish) {
        if (!CanSave || SelectedCategory == null) return;
        IsBusy = true;
        StatusMessage = publish ? "正在发布…" : "正在保存草稿…";
        try {
            var result = await _library.UpdateAsync(PostId, new PostUpdateDto {
                Id = PostId,
                Title = Title.Trim(),
                Summary = Summary,
                Slug = string.IsNullOrWhiteSpace(Slug) ? null : Slug.Trim(),
                Content = Content,
                CategoryId = SelectedCategory.Id,
                IsPublish = publish
            });
            if (!result.Success || result.Post == null) {
                GuiHost.ToastError("保存失败", result.ErrorMessage ?? "保存失败");
                StatusMessage = result.ErrorMessage ?? "保存失败";
                return;
            }

            _changed = true;
            ApplyPost(result.Post, result.PostUrl);
            var label = publish ? "已发布" : "已存为草稿";
            GuiHost.ToastSuccess(label, Title);
            StatusMessage = label;
        }
        finally {
            IsBusy = false;
        }
    }

    private async Task LoadCategoriesAsync() {
        var result = await _categories.GetCategoriesAsync();
        Categories.Clear();
        if (!result.Success || result.Categories == null) return;
        foreach (var category in CategoryApplicationService.Flatten(result.Categories)) {
            Categories.Add(category);
        }
    }

    private void ApplyPost(BlogPost post, string? url) {
        Title = post.Title;
        Summary = post.Summary ?? string.Empty;
        Slug = post.Slug ?? string.Empty;
        Content = post.Content ?? string.Empty;
        IsPublish = post.IsPublish;
        LastUpdateTime = post.LastUpdateTime;
        PostUrl = url ?? _library.BuildPostUrl(post);
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == post.CategoryId)
            ?? post.Category
            ?? SelectedCategory;
        _loadedSnapshot = Snapshot();
        NotifyEditorState();
    }

    private string Snapshot() => string.Join('\u001f', Title, Summary, Slug, Content, SelectedCategory?.Id.ToString() ?? "", IsPublish);

    private void NotifyEditorState() {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(StatusText));
    }

    private string SuggestFileName() {
        var name = string.IsNullOrWhiteSpace(Slug) ? Title : Slug;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return string.IsNullOrWhiteSpace(name) ? "article.md" : name.Trim() + ".md";
    }
}
