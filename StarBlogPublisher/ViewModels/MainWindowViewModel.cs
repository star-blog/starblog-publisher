using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using StarBlogPublisher.Views;
using ReactiveUI;
using System.Collections.ObjectModel;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using System.Diagnostics;
using System.Text;
using CodeLab.Share.Extensions;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Utils;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {
    private readonly AuthApplicationService _authService = new(
        AppSettings.Instance, GlobalState.Instance, ApiService.Instance
    );
    private readonly CategoryApplicationService _categoryService;
    private readonly ArticlePublishApplicationService _publishService;
    private readonly AiApplicationService _aiService;

    [RelayCommand]
    private void ShowWordCloud() {
        if (!IsLoggedIn) return;
        var window = new WordCloudWindow();
        window.ShowDialog(App.MainWindow);
    }

    [RelayCommand]
    private async Task ShowAddCategory() {
        if (!IsLoggedIn) return;
        var window = new AddCategoryWindow();
        await window.ShowDialog(App.MainWindow);
    }

    /// <summary>
    /// 分析文章图片命令
    /// </summary>
    [RelayCommand]
    private void AnalyzeImages() {
        if (string.IsNullOrEmpty(_currentFilePath) || string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "请先选择并加载Markdown文件";
            return;
        }

        try {
            StatusMessage = "正在分析文章中的图片...";
            var imagePaths = _publishService.AnalyzeImages(_currentFilePath, ArticleContent);

            if (imagePaths.Length == 0) {
                StatusMessage = "文章中未找到本地图片";
                return;
            }

            var viewModel = new ImageGalleryWindowViewModel();
            var window = new ImageGalleryWindow(viewModel);
            viewModel.SetWindow(window);
            viewModel.LoadImages(imagePaths);

            window.ShowDialog(App.MainWindow);
            StatusMessage = $"图片分析完成，共找到 {imagePaths.Length} 张图片";
        }
        catch (Exception ex) {
            StatusMessage = $"分析图片失败: {ex.Message}";
        }
    }

    public MainWindowViewModel() {
        _categoryService = new CategoryApplicationService(ApiService.Instance, _authService);
        _publishService = new ArticlePublishApplicationService(ApiService.Instance, _authService, AppSettings.Instance);
        _aiService = new AiApplicationService(AiService.Instance, AppSettings.Instance);

        // 订阅全局状态变更事件
        GlobalState.Instance.StateChanged += OnGlobalStateChanged;

        // 初始化登录状态
        UpdateLoginState();

        // 如果有凭据，自动登录
        if (_authService.HasCredentials) {
            _ = Login();
        }

        // 从设置中加载主题
        IsDarkTheme = AppSettings.Instance.IsDarkTheme;
        var app = Application.Current;
        if (app != null) {
            app.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        // 初始化AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
        
        // 初始化标题优化模板
        InitializeTitleOptimizationTemplates();
    }
    
    /// <summary>
    /// 初始化标题优化模板列表
    /// </summary>
    private void InitializeTitleOptimizationTemplates()
    {
        TitleOptimizationTemplates.Clear();
        foreach (var template in PromptTemplates.TitleOptimizationTemplates)
        {
            TitleOptimizationTemplates.Add(template);
        }
        
        // 设置默认选中的模板
        SelectedTitleOptimizationTemplate = TitleOptimizationTemplates.FirstOrDefault(t => t.IsDefault) 
                                          ?? TitleOptimizationTemplates.FirstOrDefault();
    }

    // 软件版本信息
    [ObservableProperty] private string _softwareVersion = "版本: 1.0.0";

    // 主题设置
    [ObservableProperty] private bool _isDarkTheme = false;

    // 文章标题和描述
    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _articleDescription = string.Empty;
    [ObservableProperty] private string _articleSlug = string.Empty;
    [ObservableProperty] private string _articleKeywords = string.Empty;

    // 是否正在润色标题
    [ObservableProperty] private bool _isRefiningTitle = false;

    // AI功能是否启用
    [ObservableProperty] private bool _isAIEnabled = false;

    // 标题优化模板选项
    [ObservableProperty] private ObservableCollection<TitleOptimizationTemplate> _titleOptimizationTemplates = new();
    [ObservableProperty] private TitleOptimizationTemplate? _selectedTitleOptimizationTemplate;

    // 文章内容
    [ObservableProperty] private string _articleContent = "";

    // 当前打开的文件路径
    private string? _currentFilePath;

    // 分类相关
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private bool _isRefreshingCategories = false;

    // 发布状态
    [ObservableProperty] private bool _isPublishing = false;
    [ObservableProperty] private double _publishProgress = 0;
    [ObservableProperty] private string _statusMessage = "准备就绪";
    [ObservableProperty] private bool _canPublish = false;

    // 登录状态
    [ObservableProperty] private bool _isLoggedIn = false;
    [ObservableProperty] private bool _hasCredentials = false;
    [ObservableProperty] private string _loginStatusMessage = "未登录";

    // 切换主题命令
    [RelayCommand]
    private void ToggleTheme() {
        IsDarkTheme = !IsDarkTheme;
        var app = Application.Current;
        if (app != null) {
            app.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        // 保存主题设置
        AppSettings.Instance.IsDarkTheme = IsDarkTheme;
        AppSettings.Instance.Save();
    }

    // 选择文件命令
    [RelayCommand]
    private async Task SelectFile() {
        var topLevel = TopLevel.GetTopLevel(App.MainWindow);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择Markdown文件",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } } }
        });

        if (files.Count > 0) {
            var file = files[0];
            try {
                await using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                ArticleContent = await reader.ReadToEndAsync();
                ArticleTitle = Path.GetFileNameWithoutExtension(file.Name);

                // 保存当前文件路径
                _currentFilePath = file.Path.LocalPath;

                // 如果AI功能已开启，使用AI生成相关信息
                if (AppSettings.Instance.EnableAI) {
                    // 生成简介
                    await RegenerateDescription();
                    // 生成Slug
                    await GenerateSlug();
                    StatusMessage = $"已加载文件: {file.Name}（AI已生成简介和Slug）";
                }
                else {
                    ArticleDescription = ArticleContent.Limit(100);
                    StatusMessage = $"已加载文件: {file.Name}";
                }

                CanPublish = true;
            }
            catch {
                StatusMessage = "文件加载失败";
            }
        }
    }

    // 复制内容命令
    [RelayCommand]
    private async Task CopyContent() {
        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可复制";
            return;
        }

        try {
            await App.MainWindow.Clipboard.SetTextAsync(ArticleContent);
            StatusMessage = "内容已复制到剪贴板";
        }
        catch (Exception ex) {
            StatusMessage = $"复制失败: {ex.Message}";
        }
    }

    // 显示封面提示词窗口
    [RelayCommand]
    private async Task ShowCoverPromptWindow() {
        var viewmodel = new CoverPromptWindowViewModel {
            ArticleTitle = ArticleTitle,
            ArticleContent = ArticleContent,
            ArticleDescription = ArticleDescription
        };
        var window = new CoverPromptWindow {
            DataContext = viewmodel
        };
        await window.ShowDialog(App.MainWindow);
    }

    // 发布文章命令
    [RelayCommand]
    private async Task Publish() {
        if (!IsLoggedIn) {
            StatusMessage = "请先登录";
            var loginMsgBox = MessageBoxManager.GetMessageBoxStandard(
                "登录提示", "您需要先登录才能发布文章。",
                ButtonEnum.OkCancel, Icon.Warning);
            if (await loginMsgBox.ShowWindowDialogAsync(App.MainWindow) == ButtonResult.Ok) {
                await Login();
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentFilePath)) { StatusMessage = "没有选择文件"; return; }
        if (string.IsNullOrEmpty(ArticleContent)) { StatusMessage = "没有内容可发布"; return; }
        if (SelectedCategory == null) { StatusMessage = "请选择文章分类"; return; }

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
            StatusMessage = "发布完成";

            var publishedMsgBox = MessageBoxManager.GetMessageBoxStandard(
                "发布完成", "文章已经成功发布到博客，点击确定跳转查看",
                ButtonEnum.OkCancel, Icon.Success);
            if (await publishedMsgBox.ShowWindowDialogAsync(App.MainWindow) == ButtonResult.Ok) {
                var url = result.Post.Slug != null
                    ? $"{ApiService.Instance.BaseUrl}/p/{result.Post.Slug}"
                    : $"{ApiService.Instance.BaseUrl}/Blog/Post/{result.Post.Id}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        else {
            StatusMessage = result.ErrorMessage ?? "发布失败";
        }

        IsPublishing = false;
    }

    [RelayCommand]
    private async Task ShowAbout() {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(App.MainWindow);
    }

    [RelayCommand]
    private async Task ShowAiSettings() {
        var aiSettingsWindow = new AiSettingsWindow();
        await aiSettingsWindow.ShowDialog(App.MainWindow);

        // 更新AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
    }

    [RelayCommand]
    private async Task ShowSettings() {
        var settingsWindow = new SettingsWindow();
        await settingsWindow.ShowDialog(App.MainWindow);

        // 设置窗口关闭后，更新登录状态
        UpdateLoginState();

        // 更新AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
    }

    // 登录命令
    [RelayCommand]
    private async Task Login() {
        if (!_authService.HasCredentials) {
            StatusMessage = "请先配置用户名和密码";
            await ShowSettings();
            return;
        }

        StatusMessage = "正在登录...";
        var result = await _authService.LoginAsync();
        StatusMessage = result.Success ? "登录成功" : result.ErrorMessage ?? "登录失败";
    }

    // 登出命令
    [RelayCommand]
    private void Logout() {
        _authService.Logout();
        StatusMessage = "已登出";
    }

    // 预览文章命令
    [RelayCommand]
    private async Task Preview() {
        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可预览";
            return;
        }

        var previewWindow = new PreviewWindow(ArticleContent);
        await previewWindow.ShowDialog(App.MainWindow);
        StatusMessage = "预览已关闭";
    }

    // 全局状态变更事件处理
    private void OnGlobalStateChanged(object? sender, EventArgs e) {
        // 在UI线程上更新状态
        Dispatcher.UIThread.Post(UpdateLoginState);
    }

    // 刷新分类命令
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
        }

        IsRefreshingCategories = false;
    }

    // 更新登录状态
    private void UpdateLoginState() {
        bool wasLoggedIn = IsLoggedIn;
        IsLoggedIn = _authService.IsLoggedIn;
        HasCredentials = _authService.HasCredentials;
        LoginStatusMessage = _authService.GetStatusMessage();

        // 如果是刚登录成功，自动刷新分类
        if (IsLoggedIn && !wasLoggedIn) {
            RefreshCategoriesCommand.Execute(null);
        }
    }

    // 重新生成文章简介命令
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
        }
    }

    // 重置标题命令
    [RelayCommand]
    private void ResetTitle() {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        ArticleTitle = Path.GetFileNameWithoutExtension(_currentFilePath);
        StatusMessage = "已重置标题为文件名";
    }

    /// <summary>
    /// 润色文章标题命令
    /// </summary>
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
        }
        finally {
            IsRefiningTitle = false;
        }
    }

    // 生成文章关键词命令
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
                // 实时更新显示
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
        }
    }

    // 生成文章Slug命令
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
        }
    }

    /// <summary>
    /// 从AI返回的JSON格式中提取关键词（用于流式实时显示）
    /// </summary>
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
        catch { }
        return string.Empty;
    }
}