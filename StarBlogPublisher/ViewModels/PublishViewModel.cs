using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markdig;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel : PageViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly CategoryApplicationService _categoryService;
    private readonly ArticlePublishApplicationService _publishService;
    private readonly AiApplicationService _aiService;
    private string? _currentFilePath;
    private string _loadedContent = "";

    public PublishViewModel(MainWindowViewModel shell) : base("发布", Icon.Pen) {
        _shell = shell;
        _categoryService = new CategoryApplicationService(ApiService.Instance, shell.AuthService);
        _publishService = new ArticlePublishApplicationService(ApiService.Instance, shell.AuthService, AppSettings.Instance);
        _aiService = new AiApplicationService(AiService.Instance, AppSettings.Instance);
        IsAIEnabled = AppSettings.Instance.EnableAI;
        NotifyAiEnabled();
        InitializeTitleOptimizationTemplates();
        LoadEditorPreferences();
    }

    public ObservableCollection<StackBreadcrumb> Breadcrumbs { get; } = new();

    public string? CurrentFilePath => _currentFilePath;

    [ObservableProperty] private object? _activeStackPage;
    [ObservableProperty] private bool _isStackNavigating;

    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _articleDescription = string.Empty;
    [ObservableProperty] private string _articleSlug = string.Empty;
    [ObservableProperty] private string _articleKeywords = string.Empty;
    [ObservableProperty] private bool _isRefiningTitle;
    [ObservableProperty] private bool _isAIEnabled;
    [ObservableProperty] private ObservableCollection<TitleOptimizationTemplate> _titleOptimizationTemplates = new();
    [ObservableProperty] private TitleOptimizationTemplate? _selectedTitleOptimizationTemplate;
    [ObservableProperty] private string _articleContent = "";
    [ObservableProperty] private bool _hasLoadedArticle;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private bool _isRefreshingCategories;
    [ObservableProperty] private bool _isPublishing;
    [ObservableProperty] private double _publishProgress;
    [ObservableProperty] private string _statusMessage = "准备就绪";
    [ObservableProperty] private bool _canPublish;
    [ObservableProperty] private PublishResult? _lastPublishResult;
    [ObservableProperty] private PublishResultViewModel? _result;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private MarkdownEditorMode _editorMode = MarkdownEditorMode.Source;
    [ObservableProperty] private bool _isInspectorOpen = true;
    [ObservableProperty] private double _editorFontSize = 14;
    [ObservableProperty] private bool _editorWordWrap = true;
    [ObservableProperty] private bool _editorShowLineNumbers;
    [ObservableProperty] private string _categorySearchText = "";
    [ObservableProperty] private string _newKeywordText = "";
    [ObservableProperty] private ObservableCollection<Category> _filteredCategories = new();
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private int _imageCount;
    [ObservableProperty] private string _aiProviderLabel = "";
    [ObservableProperty] private bool _isLoadingDocument;
    [ObservableProperty] private bool _isPreparingAi;
    [ObservableProperty] private Uri? _previewUri;

    public ObservableCollection<string> KeywordItems { get; } = new();

    public bool HasPublishResult => LastPublishResult?.Success == true;
    public bool IsWorkspaceBusy => IsPublishing || IsLoadingDocument;
    public bool IsWorkspaceBusyIndeterminate => IsLoadingDocument && !IsPublishing;
    public string WorkspaceBusyMessage => IsPublishing ? StatusMessage : "正在打开文件...";
    public bool IsDocumentDirty => !string.Equals(ArticleContent, _loadedContent, StringComparison.Ordinal);
    public string DocumentFileName => string.IsNullOrWhiteSpace(_currentFilePath)
        ? "未命名.md"
        : Path.GetFileName(_currentFilePath);
    public string DocumentDisplayName => IsDocumentDirty ? $"{DocumentFileName} •" : DocumentFileName;
    public string SaveStatusText =>
        !HasLoadedArticle ? "未打开"
        : IsPreparingAi ? "正在生成元数据"
        : IsDocumentDirty ? "已修改"
        : "已加载";
    public string ConnectionStatusText => IsLoggedIn ? "StarBlog ● Connected" : "StarBlog ○ Offline";
    public double InspectorPaneWidth => IsInspectorOpen ? 320 : 48;
    public bool CanPreview => HasLoadedArticle && !string.IsNullOrWhiteSpace(ArticleContent);
    public bool IsSourcePaneVisible => EditorMode != MarkdownEditorMode.Preview;
    public bool IsPreviewPaneVisible => EditorMode != MarkdownEditorMode.Source;
    public bool IsSourceMode {
        get => EditorMode == MarkdownEditorMode.Source;
        set { if (value) EditorMode = MarkdownEditorMode.Source; }
    }
    public bool IsSplitMode {
        get => EditorMode == MarkdownEditorMode.Split;
        set { if (value) EditorMode = MarkdownEditorMode.Split; }
    }
    public bool IsPreviewMode {
        get => EditorMode == MarkdownEditorMode.Preview;
        set { if (value) EditorMode = MarkdownEditorMode.Preview; }
    }
    public bool IsPreviewVisible => EditorMode != MarkdownEditorMode.Source;
    public string EditorModeLabel => EditorMode switch {
        MarkdownEditorMode.Split => "分栏",
        MarkdownEditorMode.Preview => "预览",
        _ => "源码"
    };
    public GridLength SourceColumnWidth => EditorMode == MarkdownEditorMode.Preview
        ? new GridLength(0)
        : new GridLength(1, GridUnitType.Star);
    public GridLength PreviewColumnWidth => EditorMode == MarkdownEditorMode.Source
        ? new GridLength(0)
        : new GridLength(1, GridUnitType.Star);
    public GridLength SplitterColumnWidth => EditorMode == MarkdownEditorMode.Split
        ? new GridLength(4)
        : new GridLength(0);
    public bool IsPublishReady => CanPublish;
    public bool HasSelectedCategory => SelectedCategory != null;
    public string CategorySelectionText => SelectedCategory?.Text ?? "选择文章分类";
    public string CategorySelectionHint => SelectedCategory == null
        ? "发布前请选择一个分类"
        : "已选择，点击可更改分类";
    public bool ShowPublishBlockers => HasLoadedArticle && !IsPublishReady;
    public bool ShowReadyToPublish => IsPublishReady && !HasPublishResult;
    public bool NeedsLoginToPublish => HasLoadedArticle && !IsLoggedIn;
    public string PublishReadinessText {
        get {
            if (!HasLoadedArticle) {
                return "未打开文件";
            }

            var missing = new List<string>();
            if (!IsLoggedIn) {
                missing.Add("登录");
            }

            if (SelectedCategory == null) {
                missing.Add("分类");
            }

            if (string.IsNullOrWhiteSpace(ArticleTitle)) {
                missing.Add("标题");
            }

            if (string.IsNullOrWhiteSpace(ArticleContent) || string.IsNullOrWhiteSpace(_currentFilePath)) {
                missing.Add("正文");
            }

            return missing.Count == 0 ? "可发布" : $"还差：{string.Join(" · ", missing)}";
        }
    }

    partial void OnLastPublishResultChanged(PublishResult? value) {
        OnPropertyChanged(nameof(HasPublishResult));
        OnPropertyChanged(nameof(ShowReadyToPublish));
        Result = value is { Success: true } ? new PublishResultViewModel(value) : null;
    }

    public void NotifyLoginState(bool isLoggedIn) {
        IsLoggedIn = isLoggedIn;
        NotifyAiEnabled();
        NotifyPublishReadiness();
    }

    public void NotifyAiEnabled() {
        IsAIEnabled = AppSettings.Instance.EnableAI;
        AiProviderLabel = IsAIEnabled
            ? (string.IsNullOrWhiteSpace(AppSettings.Instance.AIProvider) ? "AI" : AppSettings.Instance.AIProvider)
            : "AI 关闭";
    }

    partial void OnIsInspectorOpenChanged(bool value) => OnPropertyChanged(nameof(InspectorPaneWidth));

    partial void OnIsLoggedInChanged(bool value) {
        OnPropertyChanged(nameof(ConnectionStatusText));
        NotifyPublishReadiness();
    }

    private bool _restoreInspectorAfterSplit;

    partial void OnEditorModeChanged(MarkdownEditorMode oldValue, MarkdownEditorMode newValue) {
        if (newValue == MarkdownEditorMode.Split && oldValue != MarkdownEditorMode.Split && IsInspectorOpen) {
            _restoreInspectorAfterSplit = true;
            IsInspectorOpen = false;
        }
        else if (oldValue == MarkdownEditorMode.Split && newValue != MarkdownEditorMode.Split && _restoreInspectorAfterSplit) {
            IsInspectorOpen = true;
            _restoreInspectorAfterSplit = false;
        }

        OnPropertyChanged(nameof(IsSourcePaneVisible));
        OnPropertyChanged(nameof(IsPreviewPaneVisible));
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsPreviewVisible));
        OnPropertyChanged(nameof(EditorModeLabel));
        OnPropertyChanged(nameof(SourceColumnWidth));
        OnPropertyChanged(nameof(PreviewColumnWidth));
        OnPropertyChanged(nameof(SplitterColumnWidth));
        StatusMessage = newValue switch {
            MarkdownEditorMode.Split => "分栏预览",
            MarkdownEditorMode.Preview => "正在预览文章",
            _ => "已返回编辑"
        };
    }

    partial void OnEditorFontSizeChanged(double value) => PersistEditorPreferences();

    partial void OnEditorWordWrapChanged(bool value) => PersistEditorPreferences();

    partial void OnEditorShowLineNumbersChanged(bool value) => PersistEditorPreferences();

    partial void OnArticleTitleChanged(string value) => NotifyPublishReadiness();

    partial void OnIsLoadingDocumentChanged(bool value) => NotifyWorkspaceBusy();

    partial void OnIsPreparingAiChanged(bool value) => OnPropertyChanged(nameof(SaveStatusText));

    partial void OnStatusMessageChanged(string value) {
        if (IsPublishing) {
            OnPropertyChanged(nameof(WorkspaceBusyMessage));
        }
    }

    partial void OnArticleContentChanged(string value) {
        UpdateDocumentStats();
        NotifyDocumentState();
        RefreshPreview();
    }

    partial void OnArticleKeywordsChanged(string value) => SyncKeywordItems();

    partial void OnCategoriesChanged(ObservableCollection<Category> value) => RefreshFilteredCategories();

    partial void OnCategorySearchTextChanged(string value) => RefreshFilteredCategories();

    partial void OnHasLoadedArticleChanged(bool value) {
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(CanPreview));
        NotifyPublishReadiness();
        _shell.RefreshChromeTitle();
    }

    public void OpenStackPage(object page, string title) {
        ActiveStackPage = page;
        IsStackNavigating = true;
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new StackBreadcrumb { Title = Title, Target = null });
        Breadcrumbs.Add(new StackBreadcrumb { Title = title, Target = page });
    }

    public void NavigateBreadcrumbAt(int index) {
        if (index < 0 || Breadcrumbs.Count == 0) {
            return;
        }

        if (index == 0) {
            ActiveStackPage = null;
            IsStackNavigating = false;
            Breadcrumbs.Clear();
            return;
        }

        if (index >= Breadcrumbs.Count) {
            return;
        }

        while (Breadcrumbs.Count > index + 1) {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        ActiveStackPage = Breadcrumbs[index].Target;
        IsStackNavigating = ActiveStackPage != null;
    }

    private void InitializeTitleOptimizationTemplates() {
        TitleOptimizationTemplates.Clear();
        foreach (var template in PromptTemplates.TitleOptimizationTemplates) {
            TitleOptimizationTemplates.Add(template);
        }

        SelectedTitleOptimizationTemplate = TitleOptimizationTemplates.FirstOrDefault(t => t.IsDefault)
                                            ?? TitleOptimizationTemplates.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SelectFile() {
        var topLevel = GuiHost.GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择Markdown文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }]
        });

        if (files.Count > 0) {
            await LoadFromPathAsync(files[0].Path.LocalPath, files[0].Name);
        }
    }

    public async Task LoadFromPathAsync(string path, string? displayName = null) {
        IsLoadingDocument = true;
        try {
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
            ArticleContent = content;
            _loadedContent = content;
            ArticleTitle = Path.GetFileNameWithoutExtension(path);
            LastPublishResult = null;
            _currentFilePath = path;
            RefreshPreview();
            EditorMode = MarkdownEditorMode.Source;
            UpdateDocumentStats();
            NotifyDocumentState();
            HasLoadedArticle = true;
            ArticleDescription = ArticleContent.Limit(100);
            ArticleSlug = string.Empty;
            ArticleKeywords = string.Empty;
            NotifyPublishReadiness();

            var name = displayName ?? Path.GetFileName(path);
            StatusMessage = $"已加载文件: {name}";
            GuiHost.ToastSuccess("已加载", name);
            IsLoadingDocument = false;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        }
        catch (Exception ex) {
            StatusMessage = "文件加载失败";
            GuiHost.ToastError("文件加载失败", ex.Message);
        }
        finally {
            IsLoadingDocument = false;
        }
    }

    private void RefreshPreview() {
        if (string.IsNullOrWhiteSpace(ArticleContent)) {
            PreviewUri = null;
            return;
        }

        try {
            var previewDirectory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "markdown-preview");
            Directory.CreateDirectory(previewDirectory);
            var previewPath = Path.Combine(previewDirectory, "index.html");
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()
                .Build();
            var baseHref = string.Empty;
            var articleDirectory = string.IsNullOrWhiteSpace(_currentFilePath)
                ? null
                : Path.GetDirectoryName(_currentFilePath);
            if (!string.IsNullOrWhiteSpace(articleDirectory)) {
                baseHref = new Uri(Path.EndsInDirectorySeparator(articleDirectory)
                    ? articleDirectory
                    : articleDirectory + Path.DirectorySeparatorChar).AbsoluteUri;
            }

            var body = Markdig.Markdown.ToHtml(ArticleContent, pipeline);
            var document = $$"""
                <!doctype html>
                <html lang="zh-CN">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <base href="{{WebUtility.HtmlEncode(baseHref)}}">
                  <style>
                    :root { color-scheme: light dark; font-family: Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", "Microsoft YaHei", sans-serif; }
                    body { box-sizing: border-box; max-width: 920px; margin: 0 auto; padding: 28px 36px 56px; color: #1f2328; background: #fff; font-size: 16px; line-height: 1.72; }
                    h1, h2, h3, h4, h5, h6 { line-height: 1.3; margin: 1.45em 0 .65em; color: #111827; }
                    h1 { font-size: 2em; border-bottom: 1px solid #d8dee4; padding-bottom: .35em; } h2 { font-size: 1.55em; border-bottom: 1px solid #e5e7eb; padding-bottom: .3em; } h3 { font-size: 1.25em; }
                    p, ul, ol, blockquote { margin: 0 0 1em; } li + li { margin-top: .3em; }
                    a { color: #0969da; text-decoration: none; } a:hover { text-decoration: underline; }
                    code { padding: .16em .36em; border-radius: 5px; background: #f1f3f5; color: #c7254e; font: .9em "Cascadia Code", Consolas, monospace; }
                    pre { overflow: auto; padding: 16px 18px; border-radius: 8px; background: #1f2937; color: #e5e7eb; line-height: 1.55; } pre code { padding: 0; background: transparent; color: inherit; }
                    blockquote { margin-left: 0; padding: .15em 1em; border-left: 4px solid #d0d7de; color: #57606a; background: #f6f8fa; }
                    table { display: block; width: max-content; max-width: 100%; overflow: auto; border-collapse: collapse; margin-bottom: 1em; } th, td { padding: .45em .75em; border: 1px solid #d0d7de; } th { background: #f6f8fa; }
                    img { display: block; max-width: 100%; height: auto; margin: 1em 0; border-radius: 6px; }
                    hr { height: 1px; border: 0; background: #d8dee4; margin: 2em 0; }
                    @media (prefers-color-scheme: dark) { body { color: #e6edf3; background: #0d1117; } h1,h2,h3,h4,h5,h6 { color: #f0f6fc; border-color: #30363d; } a { color: #58a6ff; } code { background: #161b22; color: #ff7b72; } blockquote, th { color: #b1bac4; background: #161b22; border-color: #3b434b; } th,td { border-color: #30363d; } hr { background: #30363d; } }
                  </style>
                </head>
                <body>{{body}}</body>
                </html>
                """;
            File.WriteAllText(previewPath, document, Encoding.UTF8);
            PreviewUri = new Uri($"{new Uri(previewPath).AbsoluteUri}?v={DateTime.UtcNow.Ticks}");
        }
        catch (Exception ex) {
            PreviewUri = null;
            StatusMessage = $"预览生成失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyContent() {
        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可复制";
            return;
        }

        try {
            var clipboard = GuiHost.GetTopLevel()?.Clipboard;
            if (clipboard == null) {
                StatusMessage = "无法访问剪贴板";
                return;
            }

            await clipboard.SetTextAsync(ArticleContent);
            StatusMessage = "内容已复制到剪贴板";
            GuiHost.ToastSuccess("复制", "内容已复制到剪贴板");
        }
        catch (Exception ex) {
            StatusMessage = $"复制失败: {ex.Message}";
            GuiHost.ToastError("复制失败", ex.Message);
        }
    }

    [RelayCommand]
    private void Preview() {
        if (!CanPreview) {
            StatusMessage = "没有内容可预览";
            GuiHost.ToastWarning("预览", "没有内容可预览");
            return;
        }

        EditorMode = EditorMode == MarkdownEditorMode.Preview
            ? MarkdownEditorMode.Source
            : MarkdownEditorMode.Preview;
    }

    [RelayCommand]
    private async Task RequestLogin() {
        await _shell.EnsureLoggedInAsync();
    }

    [RelayCommand]
    private async Task FillMetadata() {
        if (!_aiService.IsEnabled || string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "无法补全元数据：AI功能未启用或文章内容为空";
            return;
        }

        IsPreparingAi = true;
        try {
            await RegenerateDescription();
            await GenerateKeywords();
            await GenerateSlug();
            StatusMessage = "AI已补全摘要、关键词和 Slug";
        }
        finally {
            IsPreparingAi = false;
        }
    }

    [RelayCommand]
    private void ShowPublishResult() {
        if (Result == null) {
            StatusMessage = "暂无可查看的发布结果";
            return;
        }

        OpenStackPage(Result, "发布结果");
    }

    [RelayCommand]
    private void ToggleInspector() {
        IsInspectorOpen = !IsInspectorOpen;
        if (EditorMode == MarkdownEditorMode.Split) {
            _restoreInspectorAfterSplit = false;
        }
    }

    [RelayCommand]
    private void SetEditorMode(string? mode) {
        if (Enum.TryParse<MarkdownEditorMode>(mode, out var parsed)) {
            EditorMode = parsed;
        }
    }

    [RelayCommand]
    private void IncreaseEditorFontSize() {
        EditorFontSize = Math.Min(22, EditorFontSize + 1);
    }

    [RelayCommand]
    private void DecreaseEditorFontSize() {
        EditorFontSize = Math.Max(11, EditorFontSize - 1);
    }

    [RelayCommand]
    private async Task Publish() {
        if (!_shell.IsUserLoggedIn) {
            StatusMessage = "请先登录";
            if (await GuiHost.ConfirmAsync("登录提示", "您需要先登录才能发布文章。")) {
                await _shell.EnsureLoggedInAsync();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_currentFilePath)) {
            StatusMessage = "没有选择文件";
            return;
        }

        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可发布";
            return;
        }

        if (SelectedCategory == null) {
            StatusMessage = "请选择文章分类";
            GuiHost.ToastWarning("发布", "请选择文章分类");
            return;
        }

        IsPublishing = true;
        PublishProgress = 0;
        StatusMessage = "正在发布...";

        var result = await _publishService.PublishAsync(
            _currentFilePath, ArticleTitle, ArticleContent, ArticleDescription,
            SelectedCategory.Id, ArticleSlug, true,
            onProgress: (step, msg) => {
                PublishProgress = step;
                StatusMessage = msg;
            });

            if (result.Success && result.Post != null) {
            PublishProgress = 100;
            if (!string.IsNullOrWhiteSpace(result.Post.Content)) {
                ArticleContent = result.Post.Content;
                _loadedContent = ArticleContent;
                StatusMessage = "发布完成，已用服务器内容更新编辑器";
            }
            else {
                StatusMessage = "发布完成";
            }

            LastPublishResult = result;
            if (Result != null) {
                OpenStackPage(Result, "发布结果");
            }
            GuiHost.ToastSuccess("发布完成", result.Post.Title ?? ArticleTitle);
        }
        else {
            StatusMessage = result.ErrorMessage ?? "发布失败";
            GuiHost.ToastError("发布失败", result.ErrorMessage ?? "发布失败");
        }

        IsPublishing = false;
    }

    [RelayCommand]
    private void ShowCoverPrompt() {
        var page = new CoverPromptViewModel {
            ArticleTitle = ArticleTitle,
            ArticleContent = ArticleContent,
            ArticleDescription = ArticleDescription
        };
        OpenStackPage(page, page.Title);
    }

    [RelayCommand]
    private void AnalyzeImages() {
        if (string.IsNullOrEmpty(_currentFilePath) || string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "请先选择并加载Markdown文件";
            GuiHost.ToastWarning("分析图片", "请先选择并加载 Markdown 文件");
            return;
        }

        try {
            StatusMessage = "正在分析文章中的图片...";
            var imagePaths = _publishService.AnalyzeImages(_currentFilePath, ArticleContent);

            if (imagePaths.Length == 0) {
                StatusMessage = "文章中未找到本地图片";
                GuiHost.ToastInfo("分析图片", "文章中未找到本地图片");
                return;
            }

            var gallery = new ImageGalleryViewModel();
            gallery.LoadImages(imagePaths);
            OpenStackPage(gallery, gallery.Title);
            StatusMessage = $"图片分析完成，共找到 {imagePaths.Length} 张图片";
        }
        catch (Exception ex) {
            StatusMessage = $"分析图片失败: {ex.Message}";
            GuiHost.ToastError("分析图片失败", ex.Message);
        }
    }

    [RelayCommand]
    private void ShowWeChatPublisher() {
        if (string.IsNullOrWhiteSpace(_currentFilePath) || string.IsNullOrWhiteSpace(ArticleContent)) {
            StatusMessage = "请先选择并加载 Markdown 文件";
            GuiHost.ToastWarning("公众号排版", "请先选择并加载 Markdown 文件");
            return;
        }

        _shell.NavigateToWeChat();
    }

    [RelayCommand]
    private async Task ShowWordCloud() {
        if (!IsLoggedIn) return;
        await GuiHost.ShowContentAsync(new WordCloudViewModel(), "词云");
    }

    [RelayCommand]
    private async Task ShowAddCategory() {
        if (!IsLoggedIn) return;
        await GuiHost.ShowContentAsync(
            new AddCategoryViewModel(() => RefreshCategoriesCommand.Execute(null)),
            "添加分类");
    }

    [RelayCommand]
    private async Task RefreshCategories() {
        IsRefreshingCategories = true;
        StatusMessage = "正在刷新分类...";

        var result = await _categoryService.GetCategoriesAsync();
        if (result.Success && result.Categories != null) {
            var selectedCategoryId = SelectedCategory?.Id;
            Categories = new ObservableCollection<Category>(result.Categories);
            // 分类树刷新会创建新的模型实例；按 ID 重新关联，避免当前选择变成过期引用。
            SelectedCategory = selectedCategoryId is int id
                ? FindCategoryById(Categories, id)
                : null;
            StatusMessage = "分类刷新成功";
        }
        else {
            StatusMessage = result.ErrorMessage ?? "分类刷新失败";
            GuiHost.ToastError("分类刷新失败", result.ErrorMessage ?? "分类刷新失败");
        }

        IsRefreshingCategories = false;
    }

    [RelayCommand]
    private void SelectCategory(Category? category) {
        SelectedCategory = category;
    }

    [RelayCommand]
    private void ClearCategory() {
        SelectedCategory = null;
    }

    [RelayCommand]
    private void AddKeyword() {
        var keyword = NewKeywordText.Trim();
        if (string.IsNullOrWhiteSpace(keyword)) {
            return;
        }

        var items = ParseKeywords(ArticleKeywords);
        if (!items.Contains(keyword, StringComparer.OrdinalIgnoreCase)) {
            items.Add(keyword);
            ArticleKeywords = string.Join(", ", items);
        }

        NewKeywordText = string.Empty;
    }

    [RelayCommand]
    private void RemoveKeyword(string? keyword) {
        if (string.IsNullOrWhiteSpace(keyword)) {
            return;
        }

        ArticleKeywords = string.Join(", ", ParseKeywords(ArticleKeywords)
            .Where(item => !string.Equals(item, keyword, StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    private async Task ReloadDocument() {
        if (string.IsNullOrWhiteSpace(_currentFilePath)) {
            return;
        }

        await LoadFromPathAsync(_currentFilePath);
    }

    [RelayCommand]
    private async Task PublishAndOpen() {
        await Publish();
        if (Result != null && !string.IsNullOrWhiteSpace(Result.ArticleUrl)) {
            Result.OpenArticleCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task CopyPublishLink() {
        var url = LastPublishResult?.PostUrl;
        if (string.IsNullOrWhiteSpace(url)) {
            StatusMessage = "暂无发布链接";
            GuiHost.ToastWarning("复制链接", "请先成功发布文章");
            return;
        }

        try {
            var clipboard = GuiHost.GetTopLevel()?.Clipboard;
            if (clipboard == null) {
                return;
            }

            await clipboard.SetTextAsync(url);
            StatusMessage = "发布链接已复制";
            GuiHost.ToastSuccess("复制链接", url);
        }
        catch (Exception ex) {
            GuiHost.ToastError("复制失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefineTitleWithTemplate(string? templateKey) {
        if (!string.IsNullOrWhiteSpace(templateKey)) {
            SelectedTitleOptimizationTemplate = TitleOptimizationTemplates
                .FirstOrDefault(item => item.Key == templateKey)
                ?? SelectedTitleOptimizationTemplate;
        }

        await RefineTitleWithAI();
    }

    [RelayCommand]
    private async Task RegenerateDescription() {
        if (!_aiService.IsEnabled || string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "无法生成简介：AI功能未启用或文章内容为空";
            return;
        }

        StatusMessage = "正在使用AI重新生成文章简介...";
        try {
            var sb = new StringBuilder();
            await foreach (var chunk in _aiService.GenerateSummaryStreamAsync(ArticleTitle, ArticleContent)) {
                sb.Append(chunk);
                ArticleDescription = sb.ToString();
            }

            StatusMessage = "AI已重新生成文章简介";
        }
        catch (Exception ex) {
            StatusMessage = $"AI重新生成简介失败: {ex.Message}";
            GuiHost.ToastError("生成简介失败", ex.Message);
        }
    }

    [RelayCommand]
    private void ResetTitle() {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        ArticleTitle = Path.GetFileNameWithoutExtension(_currentFilePath);
        StatusMessage = "已重置标题为文件名";
    }

    [RelayCommand]
    private async Task RefineTitleWithAI() {
        if (!_aiService.IsEnabled || string.IsNullOrEmpty(ArticleContent) || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法润色标题：AI功能未启用或文章内容/标题为空";
            return;
        }

        if (SelectedTitleOptimizationTemplate == null) {
            StatusMessage = "请选择标题优化模板";
            return;
        }

        IsRefiningTitle = true;
        StatusMessage = $"正在使用AI润色文章标题（{SelectedTitleOptimizationTemplate.Name}）...";
        try {
            var sb = new StringBuilder();
            await foreach (var chunk in _aiService.RefineTitleStreamAsync(
                               ArticleTitle, ArticleContent, ArticleKeywords, SelectedTitleOptimizationTemplate.Key)) {
                sb.Append(chunk);
                ArticleTitle = sb.ToString();
            }

            ArticleTitle = ArticleTitle.Trim('《', '》', '"', '"', '"', '\n');
            StatusMessage = "AI已润色文章标题";
        }
        catch (Exception ex) {
            StatusMessage = $"AI润色标题失败: {ex.Message}";
            GuiHost.ToastError("润色标题失败", ex.Message);
        }
        finally {
            IsRefiningTitle = false;
        }
    }

    [RelayCommand]
    private async Task GenerateKeywords() {
        if (!_aiService.IsEnabled || string.IsNullOrEmpty(ArticleContent) || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法生成关键词：AI功能未启用或文章内容/标题为空";
            return;
        }

        StatusMessage = "正在使用AI生成文章关键词...";
        try {
            var sb = new StringBuilder();
            await foreach (var chunk in _aiService.GenerateKeywordsStreamAsync(ArticleTitle, ArticleContent)) {
                sb.Append(chunk);
                var extracted = ExtractKeywordsFromJson(sb.ToString());
                if (!string.IsNullOrEmpty(extracted)) {
                    ArticleKeywords = extracted;
                }
            }

            var finalKeywords = ExtractKeywordsFromJson(sb.ToString());
            ArticleKeywords = finalKeywords;
            StatusMessage = !string.IsNullOrEmpty(finalKeywords) ? "AI已生成文章关键词" : "AI生成关键词格式异常，请手动编辑";
        }
        catch (Exception ex) {
            StatusMessage = $"AI生成关键词失败: {ex.Message}";
            GuiHost.ToastError("生成关键词失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task GenerateSlug() {
        if (!_aiService.IsEnabled || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法生成Slug：AI功能未启用或文章标题为空";
            return;
        }

        StatusMessage = "正在使用AI生成文章Slug...";
        try {
            var sb = new StringBuilder();
            await foreach (var chunk in _aiService.GenerateSlugStreamAsync(ArticleTitle)) {
                sb.Append(chunk);
                ArticleSlug = sb.ToString().Trim();
            }

            ArticleSlug = AiApplicationService.CleanSlug(ArticleSlug);
            StatusMessage = "AI已生成文章Slug";
        }
        catch (Exception ex) {
            StatusMessage = $"AI生成Slug失败: {ex.Message}";
            GuiHost.ToastError("生成Slug失败", ex.Message);
        }
    }

    private static string ExtractKeywordsFromJson(string jsonOutput) {
        try {
            var startIndex = jsonOutput.IndexOf('[');
            var endIndex = jsonOutput.LastIndexOf(']');
            if (startIndex >= 0 && endIndex > startIndex) {
                var jsonArray = jsonOutput.Substring(startIndex, endIndex - startIndex + 1);
                var keywords = new List<string>();
                var matches = System.Text.RegularExpressions.Regex.Matches(jsonArray, @"""([^""]+)""");
                foreach (System.Text.RegularExpressions.Match match in matches) {
                    var keyword = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(keyword) && !keyword.StartsWith("//")) {
                        keywords.Add(keyword);
                    }
                }

                return string.Join(", ", keywords);
            }
        }
        catch {
            // ignored
        }

        return string.Empty;
    }

    private void UpdateDocumentStats() {
        WordCount = string.IsNullOrEmpty(ArticleContent)
            ? 0
            : ArticleContent.Count(character => !char.IsWhiteSpace(character));
        ImageCount = string.IsNullOrEmpty(ArticleContent)
            ? 0
            : Regex.Matches(ArticleContent, @"!\[[^\]]*\]\([^)]+\)").Count;
    }

    private bool _loadingEditorPreferences;

    private void LoadEditorPreferences() {
        _loadingEditorPreferences = true;
        var settings = AppSettings.Instance;
        EditorFontSize = settings.EditorFontSize is >= 11 and <= 22 ? settings.EditorFontSize : 14;
        EditorWordWrap = settings.EditorWordWrap;
        EditorShowLineNumbers = settings.EditorShowLineNumbers;
        _loadingEditorPreferences = false;
    }

    private void PersistEditorPreferences() {
        if (_loadingEditorPreferences) {
            return;
        }

        var settings = AppSettings.Instance;
        settings.EditorFontSize = EditorFontSize;
        settings.EditorWordWrap = EditorWordWrap;
        settings.EditorShowLineNumbers = EditorShowLineNumbers;
        settings.Save();
    }

    private void NotifyDocumentState() {
        OnPropertyChanged(nameof(IsDocumentDirty));
        OnPropertyChanged(nameof(DocumentFileName));
        OnPropertyChanged(nameof(DocumentDisplayName));
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(CanPreview));
        NotifyPublishReadiness();
        _shell.RefreshChromeTitle();
    }

    private void NotifyPublishReadiness() {
        CanPublish = HasLoadedArticle
                     && IsLoggedIn
                     && SelectedCategory != null
                     && !string.IsNullOrWhiteSpace(ArticleTitle)
                     && !string.IsNullOrWhiteSpace(ArticleContent)
                     && !string.IsNullOrWhiteSpace(_currentFilePath)
                     && !IsPublishing;
        OnPropertyChanged(nameof(IsPublishReady));
        OnPropertyChanged(nameof(NeedsLoginToPublish));
        OnPropertyChanged(nameof(ShowPublishBlockers));
        OnPropertyChanged(nameof(ShowReadyToPublish));
        OnPropertyChanged(nameof(PublishReadinessText));
    }

    private void SyncKeywordItems() {
        var parsed = ParseKeywords(ArticleKeywords);
        if (KeywordItems.SequenceEqual(parsed, StringComparer.Ordinal)) {
            return;
        }

        KeywordItems.Clear();
        foreach (var item in parsed) {
            KeywordItems.Add(item);
        }
    }

    private void NotifyWorkspaceBusy() {
        OnPropertyChanged(nameof(IsWorkspaceBusy));
        OnPropertyChanged(nameof(IsWorkspaceBusyIndeterminate));
        OnPropertyChanged(nameof(WorkspaceBusyMessage));
    }

    private void RefreshFilteredCategories() {
        if (string.IsNullOrWhiteSpace(CategorySearchText)) {
            FilteredCategories = Categories;
            return;
        }

        FilteredCategories = new ObservableCollection<Category>(
            FilterCategoryTree(Categories, CategorySearchText.Trim()));
    }

    private static List<Category> FilterCategoryTree(IEnumerable<Category>? categories, string query) {
        var result = new List<Category>();
        if (categories == null) {
            return result;
        }

        foreach (var category in categories) {
            var selfMatch = category.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            var filteredChildren = category.Nodes is { Count: > 0 }
                ? FilterCategoryTree(category.Nodes, query)
                : [];

            if (selfMatch) {
                result.Add(category);
            }
            else if (filteredChildren.Count > 0) {
                result.Add(new Category {
                    Id = category.Id,
                    Text = category.Text,
                    Href = category.Href,
                    Tags = category.Tags,
                    Nodes = filteredChildren
                });
            }
        }

        return result;
    }

    private static Category? FindCategoryById(IEnumerable<Category>? categories, int id) {
        if (categories == null) {
            return null;
        }

        foreach (var category in categories) {
            if (category.Id == id) {
                return category;
            }

            var child = FindCategoryById(category.Nodes, id);
            if (child != null) {
                return child;
            }
        }

        return null;
    }

    private bool _syncingCategory;

    partial void OnSelectedCategoryChanged(Category? value) {
        if (!_syncingCategory && value != null) {
            var original = FindCategoryById(Categories, value.Id);
            if (original != null && !ReferenceEquals(original, value)) {
                _syncingCategory = true;
                SelectedCategory = original;
                _syncingCategory = false;
            }
        }

        OnPropertyChanged(nameof(HasSelectedCategory));
        OnPropertyChanged(nameof(CategorySelectionText));
        OnPropertyChanged(nameof(CategorySelectionHint));
        NotifyPublishReadiness();
    }

    partial void OnIsPublishingChanged(bool value) {
        NotifyWorkspaceBusy();
        NotifyPublishReadiness();
    }

    private static List<string> ParseKeywords(string? keywords) {
        if (string.IsNullOrWhiteSpace(keywords)) {
            return [];
        }

        return keywords
            .Split([',', '，', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
