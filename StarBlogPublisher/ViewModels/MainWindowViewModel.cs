using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 应用壳：导航、主题、登录态、全局反馈。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase {
    internal readonly AuthApplicationService AuthService = new(
        AppSettings.Instance, GlobalState.Instance, ApiService.Instance
    );

    public ArticleWorkspaceViewModel Workspace { get; }
    public PublishViewModel PublishPage => Workspace.CurrentDocument;
    public WeChatViewModel WeChatPage { get; }
    public SettingsViewModel SettingsPage { get; }
    public AboutViewModel AboutPage { get; }
    public ModelCatalogPageViewModel? ModelCatalogPage { get; private set; }

    public IReadOnlyList<PageViewModelBase> Pages { get; }

    [ObservableProperty] private PageViewModelBase? _activePage;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _hasCredentials;
    [ObservableProperty] private string _loginStatusMessage = "未登录";
    [ObservableProperty] private string _softwareVersion = ApplicationVersion.Value;
    [ObservableProperty] private string _chromeTitle = "StarBlog Publisher";
    [ObservableProperty] private string _windowTitle = "StarBlog Publisher";
    [ObservableProperty] private bool _isPaneOpen;
    [ObservableProperty] private double _titleBarHeight = 32;
    [ObservableProperty] private Thickness _titleBarContentMargin = new(0, 32, 0, 0);
    [ObservableProperty] private Thickness _titleBarTitleMargin = new(60, 0, 140, 0);

    private double _titleBarRightInset = 140;
    private bool _restoreSettingsAfterModelCatalog;
    private const double CompactPaneLength = 48;
    private const double OpenPaneLength = 220;

    public MainWindowViewModel() : this(AppHttpClients.Factory) { }

    public MainWindowViewModel(IHttpClientFactory httpClientFactory, bool initializeSession = true, string? workspaceHistoryPath = null) {
        Workspace = new ArticleWorkspaceViewModel(this, workspaceHistoryPath);
        WeChatPage = new WeChatViewModel(httpClientFactory);
        SettingsPage = new SettingsViewModel(this);
        AboutPage = new AboutViewModel();
        Pages = [Workspace, WeChatPage, SettingsPage, AboutPage];
        ActivePage = Workspace;

        if (initializeSession) {
            GlobalState.Instance.StateChanged += OnGlobalStateChanged;
            UpdateLoginState();
            if (AuthService.HasCredentials) _ = Login();
        }

        IsDarkTheme = AppSettings.Instance.IsDarkTheme;
    }

    /// <summary>
    /// 应用并持久化全局主题，同时同步侧栏与设置页开关。
    /// </summary>
    public void ApplyTheme(bool isDark) {
        if (Avalonia.Application.Current != null) {
            Avalonia.Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        if (IsDarkTheme != isDark) {
            IsDarkTheme = isDark;
        }

        if (AppSettings.Instance.IsDarkTheme != isDark) {
            AppSettings.Instance.IsDarkTheme = isDark;
            AppSettings.Instance.Save();
        }

        SettingsPage.SyncDarkTheme(isDark);
        foreach (var document in Workspace.Documents) document.RefreshPreviewForThemeChange();
    }

    public void UpdateTitleBarMetrics(double height, double rightInset) {
        if (height > 0 && Math.Abs(TitleBarHeight - height) > 0.5) {
            TitleBarHeight = height;
            TitleBarContentMargin = new Thickness(0, height, 0, 0);
        }

        if (rightInset > 0 && Math.Abs(_titleBarRightInset - rightInset) > 0.5) {
            _titleBarRightInset = rightInset;
            RefreshTitleBarTitleMargin();
        }
    }

    public void RefreshChromeTitle() {
        string title;
        if (ActivePage is ArticleWorkspaceViewModel && PublishPage.HasLoadedArticle) {
            title = PublishPage.DocumentDisplayName;
        }
        else if (ActivePage is ArticleWorkspaceViewModel) {
            title = "StarBlog Publisher";
        }
        else {
            title = ActivePage?.Title ?? "StarBlog Publisher";
        }

        if (ChromeTitle != title) {
            ChromeTitle = title;
        }

        if (WindowTitle != title) {
            WindowTitle = title;
        }
    }

    partial void OnIsPaneOpenChanged(bool value) => RefreshTitleBarTitleMargin();

    private void RefreshTitleBarTitleMargin() {
        var left = (IsPaneOpen ? OpenPaneLength : CompactPaneLength) + 12;
        TitleBarTitleMargin = new Thickness(left, 0, _titleBarRightInset, 0);
    }

    partial void OnActivePageChanged(PageViewModelBase? value) {
        if (value is WeChatViewModel weChat) {
            weChat.SyncFrom(PublishPage);
        }
        else if (value is SettingsViewModel settings) {
            if (_restoreSettingsAfterModelCatalog) {
                _restoreSettingsAfterModelCatalog = false;
            }
            else {
                settings.Reload();
            }
        }

        RefreshChromeTitle();
    }

    public void NavigateTo(PageViewModelBase page) {
        if (Pages.Contains(page)) {
            ActivePage = page;
        }
    }

    public void NavigateToWeChat() {
        WeChatPage.SyncFrom(PublishPage);
        ActivePage = WeChatPage;
    }

    public async System.Threading.Tasks.Task OpenModelCatalogAsync() {
        var page = new ModelCatalogPageViewModel(SettingsPage, ReturnToSettingsFromModelCatalog);
        ModelCatalogPage = page;
        ActivePage = page;
        await page.RefreshAsync();
    }

    private void ReturnToSettingsFromModelCatalog() {
        _restoreSettingsAfterModelCatalog = true;
        ActivePage = SettingsPage;
    }

    [RelayCommand]
    private void ToggleTheme() => ApplyTheme(!IsDarkTheme);

    [RelayCommand]
    private async System.Threading.Tasks.Task Login() {
        if (!AuthService.HasCredentials) {
            GuiHost.ToastWarning("登录", "请先在设置中配置用户名和密码");
            ActivePage = SettingsPage;
            return;
        }

        var result = await AuthService.LoginAsync();
        if (result.Success) {
            GuiHost.ToastSuccess("登录", "登录成功");
        }
        else {
            GuiHost.ToastError("登录失败", result.ErrorMessage ?? "登录失败");
        }
    }

    [RelayCommand]
    private void Logout() {
        AuthService.Logout();
        GuiHost.ToastInfo("登出", "已登出");
    }

    public bool IsUserLoggedIn => AuthService.IsLoggedIn;

    public async System.Threading.Tasks.Task EnsureLoggedInAsync() {
        if (IsUserLoggedIn) return;
        await Login();
    }

    private void OnGlobalStateChanged(object? sender, EventArgs e) {
        Dispatcher.UIThread.Post(UpdateLoginState);
    }

    private void UpdateLoginState() {
        var wasLoggedIn = IsLoggedIn;
        IsLoggedIn = AuthService.IsLoggedIn;
        HasCredentials = AuthService.HasCredentials;
        LoginStatusMessage = AuthService.GetStatusMessage();
        Workspace.EmptyDocument.NotifyLoginState(IsLoggedIn);
        foreach (var document in Workspace.Documents) document.NotifyLoginState(IsLoggedIn);

        if (IsLoggedIn && !wasLoggedIn) {
            Workspace.EmptyDocument.RefreshCategoriesCommand.Execute(null);
            foreach (var document in Workspace.Documents) document.RefreshCategoriesCommand.Execute(null);
        }
    }
}
