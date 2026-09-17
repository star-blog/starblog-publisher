using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel : PageViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly CategoryApplicationService _categoryService;
    private readonly ArticlePublishApplicationService _publishService;
    private readonly AiApplicationService _aiService;
    private string? _currentFilePath;

    public PublishViewModel(MainWindowViewModel shell) : base("发布", Symbol.Edit) {
        _shell = shell;
        _categoryService = new CategoryApplicationService(ApiService.Instance, shell.AuthService);
        _publishService = new ArticlePublishApplicationService(ApiService.Instance, shell.AuthService, AppSettings.Instance);
        _aiService = new AiApplicationService(AiService.Instance, AppSettings.Instance);
        IsAIEnabled = AppSettings.Instance.EnableAI;
        InitializeTitleOptimizationTemplates();
    }

    public ObservableCollection<PublishBreadcrumb> Breadcrumbs { get; } = new();

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
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isLoggedIn;

    public bool HasPublishResult => LastPublishResult?.Success == true;

    partial void OnLastPublishResultChanged(PublishResult? value) {
        OnPropertyChanged(nameof(HasPublishResult));
        Result = value is { Success: true } ? new PublishResultViewModel(value) : null;
    }

    public void NotifyLoginState(bool isLoggedIn) {
        IsLoggedIn = isLoggedIn;
        IsAIEnabled = AppSettings.Instance.EnableAI;
    }

    public void NotifyAiEnabled() => IsAIEnabled = AppSettings.Instance.EnableAI;

    public void OpenStackPage(object page, string title) {
        ActiveStackPage = page;
        IsStackNavigating = true;
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new PublishBreadcrumb { Title = Title, Target = null });
        Breadcrumbs.Add(new PublishBreadcrumb { Title = title, Target = page });
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
        try {
            ArticleContent = await File.ReadAllTextAsync(path, Encoding.UTF8);
            ArticleTitle = Path.GetFileNameWithoutExtension(path);
            LastPublishResult = null;
            _currentFilePath = path;
            HasLoadedArticle = true;
            SelectedTabIndex = 0;

            var name = displayName ?? Path.GetFileName(path);
            if (AppSettings.Instance.EnableAI) {
                await RegenerateDescription();
                await GenerateSlug();
                StatusMessage = $"已加载文件: {name}（AI已生成简介和Slug）";
            }
            else {
                ArticleDescription = ArticleContent.Limit(100);
                StatusMessage = $"已加载文件: {name}";
            }

            CanPublish = true;
            GuiHost.ToastSuccess("已加载", name);
        }
        catch (Exception ex) {
            StatusMessage = "文件加载失败";
            GuiHost.ToastError("文件加载失败", ex.Message);
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
        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可预览";
            GuiHost.ToastWarning("预览", "没有内容可预览");
            return;
        }

        SelectedTabIndex = 1;
        StatusMessage = "正在预览文章";
    }

    [RelayCommand]
    private void ShowPublishResult() {
        if (!HasPublishResult) {
            StatusMessage = "暂无可查看的发布结果";
            return;
        }

        SelectedTabIndex = 2;
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
                StatusMessage = "发布完成，已用服务器内容更新编辑器";
            }
            else {
                StatusMessage = "发布完成";
            }

            LastPublishResult = result;
            SelectedTabIndex = 2;
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
            Categories = new ObservableCollection<Category>(result.Categories);
            StatusMessage = "分类刷新成功";
        }
        else {
            StatusMessage = result.ErrorMessage ?? "分类刷新失败";
            GuiHost.ToastError("分类刷新失败", result.ErrorMessage ?? "分类刷新失败");
        }

        IsRefreshingCategories = false;
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
}
