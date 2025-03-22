using System;
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
using CodeLab.Share.Extensions;

namespace StarBlogPublisher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {
    public MainWindowViewModel() {
        // 订阅全局状态变更事件
        GlobalState.Instance.StateChanged += OnGlobalStateChanged;

        // 初始化登录状态
        UpdateLoginState();
    }

    // 软件版本信息
    [ObservableProperty] private string _softwareVersion = "版本: 1.0.0";

    // 主题设置
    [ObservableProperty] private bool _isDarkTheme = false;

    // 文章标题和描述
    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _articleDescription = string.Empty;

    // 文章内容
    [ObservableProperty] private string _articleContent = "";

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
                using var stream = await file.OpenReadAsync();
                using var reader = new StreamReader(stream);
                ArticleContent = await reader.ReadToEndAsync();
                ArticleTitle = Path.GetFileNameWithoutExtension(file.Name);
                ArticleDescription = ArticleContent.Limit(100);
                StatusMessage = $"已加载文件: {file.Name}";
                CanPublish = true;
            }
            catch {
                StatusMessage = "文件加载失败";
            }
        }
    }

    // 发布文章命令
    [RelayCommand]
    private async Task Publish() {
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

            return;
        }

        if (string.IsNullOrEmpty(ArticleContent)) {
            StatusMessage = "没有内容可发布";
            return;
        }

        IsPublishing = true;
        PublishProgress = 0;
        StatusMessage = "正在发布...";

        // 模拟发布过程
        for (int i = 0; i <= 10; i++) {
            PublishProgress = i * 10;
            await Task.Delay(300); // 模拟网络延迟
        }

        IsPublishing = false;
        StatusMessage = "发布完成";
        var publishedMsgBox = MessageBoxManager.GetMessageBoxStandard(
            "发布完成",
            "文章已经成功发布到博客，点击确定跳转查看",
            ButtonEnum.OkCancel,
            Icon.Success
        );

        var publishedMsgBoxResult = await publishedMsgBox.ShowWindowDialogAsync(App.MainWindow);
        if (publishedMsgBoxResult == ButtonResult.Ok) {
            // todo 打开博客文章链接
        }
    }

    [RelayCommand]
    private async Task ShowAbout() {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(App.MainWindow);
    }

    [RelayCommand]
    private async Task ShowSettings() {
        var settingsWindow = new SettingsWindow();
        await settingsWindow.ShowDialog(App.MainWindow);

        // 设置窗口关闭后，更新登录状态
        UpdateLoginState();
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

            Console.WriteLine($"token: {resp.Data?.Token}, expiration: {resp.Data?.Expiration}");

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
}