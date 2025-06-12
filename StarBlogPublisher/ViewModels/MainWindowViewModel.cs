using System;
using System.Collections.Generic;
using System.IO;
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
using System.Diagnostics;
using System.Text;
using CodeLab.Share.Extensions;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Utils;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {
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

    public MainWindowViewModel() {
        // 订阅全局状态变更事件
        GlobalState.Instance.StateChanged += OnGlobalStateChanged;

        // 初始化登录状态
        UpdateLoginState();

        // 如果有凭据，自动登录
        if (HasCredentials) {
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

    // 发布文章命令
    [RelayCommand]
    private async Task Publish() {
        // 验证发布前提条件
        if (!await ValidatePublishPrerequisites()) {
            return;
        }

        IsPublishing = true;
        PublishProgress = 0;
        StatusMessage = "正在发布...";

        try {
            // 创建博客文章对象
            var blogPost = new BlogPost {
                Title = ArticleTitle,
                Content = ArticleContent,
                Summary = ArticleDescription,
                IsPublish = true
            };

            // 第一步：创建文章，获取ID
            var createResp = await CreateArticle(blogPost);
            if (createResp?.Data == null) {
                throw new Exception($"创建文章失败: {createResp?.Message ?? "未知错误"}");
            }

            // 获取创建后的文章ID
            blogPost.Id = createResp.Data.Id;
            PublishProgress = 30;

            // 第二步：处理Markdown内容中的图片
            var processedContent = await ProcessArticleImages(blogPost);

            // 第三步：如果内容有变化，更新文章
            if (processedContent != ArticleContent) {
                await UpdateArticleContent(blogPost, processedContent, createResp.Data);
            }

            // 第四步：完成发布并提示用户
            await FinishPublishing(createResp.Data);
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
            StatusMessage = $"发布失败: {ex.Message}";
        }
        finally {
            IsPublishing = false;
        }
    }

    /// <summary>
    /// 完成发布并提示用户
    /// </summary>
    /// <param name="createdPost">创建的文章数据</param>
    /// <returns></returns>
    private async Task FinishPublishing(BlogPost createdPost) {
        PublishProgress = 100;
        StatusMessage = "发布完成";

        var publishedMsgBox = MessageBoxManager.GetMessageBoxStandard(
            "发布完成",
            "文章已经成功发布到博客，点击确定跳转查看",
            ButtonEnum.OkCancel,
            Icon.Success
        );

        var publishedMsgBoxResult = await publishedMsgBox.ShowWindowDialogAsync(App.MainWindow);
        if (publishedMsgBoxResult == ButtonResult.Ok) {
            // 打开博客文章链接
            var url = createdPost.Slug != null
                ? $"{ApiService.Instance.BaseUrl}/p/{createdPost.Slug}"
                : $"{ApiService.Instance.BaseUrl}/Blog/Post/{createdPost.Id}";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    /// <summary>
    /// 验证发布前的必要条件
    /// </summary>
    /// <returns>是否满足所有发布条件</returns>
    private async Task<bool> ValidatePublishPrerequisites() {
        if (!IsLoggedIn) {
            StatusMessage = "请先登录";

            // 弹出消息框提示用户登录
            var loginRequiredMsgBox = MessageBoxManager.GetMessageBoxStandard(
                "登录提示",
                "您需要先登录才能发布文章。",
                ButtonEnum.OkCancel,
                Icon.Warning
            );

            var loginMsgBoxResult = await loginRequiredMsgBox.ShowWindowDialogAsync(App.MainWindow);
            if (loginMsgBoxResult == ButtonResult.Ok) {
                // 用户点击确定，执行登录操作
                await Login();
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(_currentFilePath)) {
            StatusMessage = "没有选择文件";
            return false;
        }

        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可发布";
            return false;
        }

        if (SelectedCategory == null) {
            StatusMessage = "请选择文章分类";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 创建文章
    /// </summary>
    /// <param name="blogPost">博客文章对象</param>
    /// <returns>API响应</returns>
    private async Task<CodeLab.Share.ViewModels.Response.ApiResponse<BlogPost>> CreateArticle(BlogPost blogPost) {
        PublishProgress = 10;
        StatusMessage = "正在创建文章...";

        return await ApiService.Instance.BlogPost.Add(new PostCreationDto {
            Title = blogPost.Title,
            Content = blogPost.Content,
            Summary = blogPost.Summary,
            CategoryId = SelectedCategory!.Id,
            Slug = string.IsNullOrWhiteSpace(ArticleSlug) ? null : ArticleSlug
        });
    }

    /// <summary>
    /// 处理文章中的图片
    /// </summary>
    /// <param name="blogPost">博客文章对象</param>
    /// <returns>处理后的文章内容</returns>
    private async Task<string> ProcessArticleImages(BlogPost blogPost) {
        StatusMessage = "正在处理文章中的图片...";
        var markdownProcessor = new MarkdownProcessor(_currentFilePath!, blogPost);

        // 订阅图片上传进度事件
        markdownProcessor.ImageUploadProgress += (uploaded, total) => {
            PublishProgress = 30 + (uploaded * 50.0 / total); // 30-80%的进度区间用于图片上传
            StatusMessage = $"正在上传图片 ({uploaded}/{total})...";
        };

        var processedContent = await markdownProcessor.MarkdownParse();
        PublishProgress = 80;
        return processedContent;
    }

    /// <summary>
    /// 更新文章内容
    /// </summary>
    /// <param name="blogPost">博客文章对象</param>
    /// <param name="processedContent">处理后的内容</param>
    /// <param name="createdPost">创建后的文章数据</param>
    /// <returns></returns>
    private async Task UpdateArticleContent(BlogPost blogPost, string processedContent, BlogPost createdPost) {
        StatusMessage = "正在更新文章内容...";
        // 调用更新文章API
        var updateDto = new PostUpdateDto {
            Id = blogPost.Id,
            Title = blogPost.Title,
            Content = processedContent,
            Summary = blogPost.Summary,
            CategoryId = SelectedCategory!.Id,
            IsPublish = true,
            Slug = string.IsNullOrWhiteSpace(ArticleSlug) ? createdPost.Slug : ArticleSlug,
            Status = createdPost.Status
        };

        var updateResp = await ApiService.Instance.BlogPost.Update(blogPost.Id, updateDto);

        if (!updateResp.Successful || updateResp.Data == null) {
            throw new Exception($"更新文章内容失败: {updateResp?.Message ?? "未知错误"}");
        }

        StatusMessage = "正在重新获取文章详情";
        var detailResp = await ApiService.Instance.BlogPost.Get(blogPost.Id);

        if (!string.IsNullOrWhiteSpace(detailResp.Data?.Content)) {
            ArticleContent = detailResp.Data.Content;
            StatusMessage = "已更新文章内容";
        }
    }

    /// <summary
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
        if (!HasCredentials) {
            StatusMessage = "请先配置用户名和密码";
            await ShowSettings();
            return;
        }

        StatusMessage = "正在登录...";

        try {
            // 登录过程和获取JWT令牌
            var resp = await ApiService.Instance.Auth.Login(new LoginUser {
                Username = AppSettings.Instance.Username,
                Password = AppSettings.Instance.Password
            });

            if (string.IsNullOrWhiteSpace(resp.Data?.Token)) {
                StatusMessage = $"登录失败: {resp.Message}";
                return;
            }

            // 更新全局状态
            GlobalState.Instance.SetLoggedIn(resp.Data.Token);

            StatusMessage = "登录成功";
        }
        catch (Exception ex) {
            StatusMessage = $"登录失败: {ex.Message}";
        }
    }

    // 登出命令
    [RelayCommand]
    private void Logout() {
        GlobalState.Instance.Logout();
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
        if (!IsLoggedIn) {
            StatusMessage = "请先登录";
            return;
        }

        IsRefreshingCategories = true;
        StatusMessage = "正在刷新分类...";

        try {
            // 清空现有分类
            Categories.Clear();

            // 从服务器获取分类数据的
            var resp = await ApiService.Instance.Categories.GetNodes();

            if (resp.Data == null) {
                StatusMessage = $"分类列表为空：{resp.Message}";
                return;
            }

            Categories = new ObservableCollection<Category>(resp.Data);

            StatusMessage = "分类刷新成功";
        }
        catch (Exception ex) {
            StatusMessage = $"分类刷新失败: {ex.Message}";
        }
        finally {
            IsRefreshingCategories = false;
        }
    }

    // 更新登录状态
    private void UpdateLoginState() {
        var globalState = GlobalState.Instance;
        bool wasLoggedIn = IsLoggedIn;
        IsLoggedIn = globalState.IsLoggedIn;
        HasCredentials = globalState.HasCredentials();

        if (IsLoggedIn) {
            LoginStatusMessage = "已登录";

            // 如果是刚登录成功，自动刷新分类
            if (!wasLoggedIn) {
                RefreshCategoriesCommand.Execute(null);
            }
        }
        else if (HasCredentials) {
            LoginStatusMessage = "未登录 (已配置凭据)";
        }
        else {
            LoginStatusMessage = "未登录 (未配置凭据)";
        }
    }

    // 重新生成文章简介命令
    [RelayCommand]
    private async Task RegenerateDescription() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "无法生成简介：AI功能未启用或文章内容为空";
            return;
        }

        StatusMessage = "正在使用AI重新生成文章简介...";
        try {
            var prompt = PromptBuilder
                .Create(PromptTemplates.ArticleDesc2)
                .AddParameter("title", ArticleTitle)
                .AddParameter("content", ArticleContent)
                .Build();
            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var description = new StringBuilder();

            await foreach (var update in textStreamAsync) {
                description.Append(update.Text);
                ArticleDescription = description.ToString();
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

    // 润色文章标题命令
    [RelayCommand]
    private async Task RefineTitleWithAI() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent) || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法润色标题：AI功能未启用或文章内容/标题为空";
            return;
        }

        IsRefiningTitle = true;
        StatusMessage = "正在使用AI润色文章标题...";
        try {
            var prompt = PromptBuilder
                .Create(PromptTemplates.RefineTitle)
                .AddParameter("title", ArticleTitle)
                .AddParameter("keywords", ArticleKeywords)
                .AddParameter("content", ArticleContent)
                .Build();
            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var refinedTitle = new System.Text.StringBuilder();

            await foreach (var update in textStreamAsync) {
                refinedTitle.Append(update.Text);
                ArticleTitle = refinedTitle.ToString();
            }

            ArticleTitle = ArticleTitle.Trim('《', '》', '\"', '"', '"', '\n');

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
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent) || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法生成关键词：AI功能未启用或文章内容/标题为空";
            return;
        }

        StatusMessage = "正在使用AI生成文章关键词...";
        try {
            var prompt = PromptBuilder
                .Create(PromptTemplates.ExtraceKeywords)
                .AddParameter("title", ArticleTitle)
                .AddParameter("content", ArticleContent)
                .Build();

            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var generatedKeywords = new System.Text.StringBuilder();

            await foreach (var update in textStreamAsync) {
                generatedKeywords.Append(update.Text);
                // 实时更新显示原始AI输出
                var rawOutput = generatedKeywords.ToString();
                // 尝试提取JSON数组中的关键词
                var extractedKeywords = ExtractKeywordsFromJson(rawOutput);
                if (!string.IsNullOrEmpty(extractedKeywords)) {
                    ArticleKeywords = extractedKeywords;
                }
            }

            Console.WriteLine($"keywords: {generatedKeywords}");

            // 最终处理，确保格式正确
            var finalKeywords = ExtractKeywordsFromJson(generatedKeywords.ToString());
            if (!string.IsNullOrEmpty(finalKeywords)) {
                ArticleKeywords = finalKeywords;
                StatusMessage = "AI已生成文章关键词";
            } else {
                StatusMessage = "AI生成关键词格式异常，请手动编辑";
            }
        }
        catch (Exception ex) {
            StatusMessage = $"AI生成关键词失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 从AI返回的JSON格式中提取关键词
    /// </summary>
    /// <param name="jsonOutput">AI返回的原始输出</param>
    /// <returns>提取的关键词字符串，用逗号分隔</returns>
    private string ExtractKeywordsFromJson(string jsonOutput) {
        try {
            // 查找JSON数组的开始和结束
            var startIndex = jsonOutput.IndexOf('[');
            var endIndex = jsonOutput.LastIndexOf(']');
            
            if (startIndex >= 0 && endIndex > startIndex) {
                var jsonArray = jsonOutput.Substring(startIndex, endIndex - startIndex + 1);
                
                // 简单的JSON解析，提取引号内的内容
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
        catch (Exception ex) {
            // 如果JSON解析失败，返回空字符串
            Console.WriteLine($"ExtractKeywordsFromJson Failed: {ex.Message}");
        }
        
        return string.Empty;
    }
    
    // 生成文章Slug命令
    [RelayCommand]
    private async Task GenerateSlug() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleTitle)) {
            StatusMessage = "无法生成Slug：AI功能未启用或文章标题为空";
            return;
        }

        StatusMessage = "正在使用AI生成文章Slug...";
        try {
            var prompt = PromptBuilder
                .Create(PromptTemplates.ArticleSlug)
                .AddParameter("title", ArticleTitle)
                .Build();

            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var generatedSlug = new System.Text.StringBuilder();

            await foreach (var update in textStreamAsync) {
                generatedSlug.Append(update.Text);
                ArticleSlug = generatedSlug.ToString().Trim();
            }

            // 清理slug，确保符合规范
            ArticleSlug = CleanSlug(ArticleSlug);

            StatusMessage = "AI已生成文章Slug";
        }
        catch (Exception ex) {
            StatusMessage = $"AI生成Slug失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 清理Slug，确保符合URL友好格式
    /// </summary>
    /// <param name="slug">原始slug</param>
    /// <returns>清理后的slug</returns>
    private string CleanSlug(string slug) {
        // 移除非字母数字连字符的字符
        var cleaned = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        // 替换连续的连字符为单个连字符
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\-+", "-");
        // 移除开头和结尾的连字符
        cleaned = cleaned.Trim('-');
        // 确保不超过50个字符
        if (cleaned.Length > 50) {
            cleaned = cleaned.Substring(0, 50).TrimEnd('-');
        }

        return cleaned;
    }
}