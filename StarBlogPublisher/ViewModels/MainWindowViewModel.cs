using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using System.Net.Http;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;
using AppThemeMode = StarBlogPublisher.Models.ThemeMode;

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

    private WeChatViewModel? _weChatPage;
    private SettingsViewModel? _settingsPage;
    private AboutViewModel? _aboutPage;
    private IHttpClientFactory? _httpClientFactory;
    private bool _initializeSession;
    private bool _startupLoginScheduled;

    public WeChatViewModel WeChatPage => _weChatPage ??= new WeChatViewModel(HttpClientFactory);
    public SettingsViewModel SettingsPage => _settingsPage ??= CreateSettingsPage();
    public AboutViewModel AboutPage => _aboutPage ??= new AboutViewModel();

    public ModelCatalogPageViewModel? ModelCatalogPage { get; private set; }

    private readonly ShellPageNavItem _weChatNav;
    private readonly ShellPageNavItem _settingsNav;
    private readonly ShellPageNavItem _aboutNav;

    public IReadOnlyList<INavigationMenuItem> NavigationItems { get; }
    public ShellFooterNavItem ThemeFooterItem { get; }
    public ShellFooterNavItem AccountFooterItem { get; }
    public IReadOnlyList<ShellFooterNavItem> FooterNavItems { get; }
    public bool IsWorkspaceActive => ActivePage == Workspace;
    public PageViewModelBase? SecondaryPage => IsWorkspaceActive ? null : ActivePage;

    private PageViewModelBase? _activePage;
    private bool _checkingSettingsNavigation;
    public PageViewModelBase? ActivePage {
        get => _activePage;
        set {
            if (_activePage == value) return;
            if (_checkingSettingsNavigation) return;
            if ((_activePage is SettingsViewModel || _activePage is ModelCatalogPageViewModel)
                && value is not SettingsViewModel && value is not ModelCatalogPageViewModel
                && SettingsPage.HasChanges) {
                _ = LeaveSettingsAsync(value);
                return;
            }
            SetActivePage(value);
        }
    }

    private void SetActivePage(PageViewModelBase? value) {
        if (SetProperty(ref _activePage, value, nameof(ActivePage))) {
            OnActivePageChanged(value);
            OnPropertyChanged(nameof(SelectedNavigationItem));
        }
    }

    private async System.Threading.Tasks.Task LeaveSettingsAsync(PageViewModelBase? destination) {
        _checkingSettingsNavigation = true;
        try {
            if (await SettingsPage.CanLeaveAsync()) SetActivePage(destination);
            else OnPropertyChanged(nameof(ActivePage));
        }
        finally { _checkingSettingsNavigation = false; }
    }
    [ObservableProperty] private AppThemeMode _themeMode;
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

    public MainWindowViewModel() : this(httpClientFactory: null) { }

    public MainWindowViewModel(IHttpClientFactory? httpClientFactory, bool initializeSession = true, string? workspaceHistoryPath = null) {
        _httpClientFactory = httpClientFactory;
        _initializeSession = initializeSession;
        Workspace = new ArticleWorkspaceViewModel(this, workspaceHistoryPath);
        _weChatNav = new ShellPageNavItem(ShellPageId.WeChat, "公众号排版", Icon.Mail);
        _settingsNav = new ShellPageNavItem(ShellPageId.Settings, "设置", Icon.Settings);
        _aboutNav = new ShellPageNavItem(ShellPageId.About, "关于", Icon.Info);
        NavigationItems = [Workspace, _weChatNav, _settingsNav, _aboutNav];
        ThemeFooterItem = new ShellFooterNavItem { Tag = "theme", Title = "主题" };
        AccountFooterItem = new ShellFooterNavItem { Tag = "account", Title = "登录" };
        FooterNavItems = [ThemeFooterItem, AccountFooterItem];
        ActivePage = Workspace;

        if (initializeSession) {
            GlobalState.Instance.StateChanged += OnGlobalStateChanged;
            UpdateLoginState();
        }

        ThemeMode = AppSettings.Instance.ThemeMode;
        IsDarkTheme = AppThemeService.ResolveEffectiveIsDark(ThemeMode);
        if (Avalonia.Application.Current != null) {
            Avalonia.Application.Current.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }
        RefreshFooterNavItems();
        InitializeCommands();
    }

    private IHttpClientFactory HttpClientFactory => _httpClientFactory ??= AppHttpClients.Factory;

    public INavigationMenuItem? SelectedNavigationItem {
        get => ActivePage switch {
            ArticleWorkspaceViewModel => Workspace,
            WeChatViewModel => _weChatNav,
            SettingsViewModel => _settingsNav,
            AboutViewModel => _aboutNav,
            ModelCatalogPageViewModel => _settingsNav,
            _ => Workspace,
        };
        set {
            if (value == null || _checkingSettingsNavigation) {
                return;
            }

            PageViewModelBase? target = value switch {
                ArticleWorkspaceViewModel workspace => workspace,
                ShellPageNavItem nav => EnsureShellPage(nav.PageId),
                _ => null,
            };
            if (target != null) {
                ActivePage = target;
            }
        }
    }

    public void OnMainWindowOpened() {
        if (_startupLoginScheduled || !_initializeSession) {
            return;
        }

        _startupLoginScheduled = true;
        if (AuthService.HasCredentials) {
            _ = Login();
        }
    }

    public async System.Threading.Tasks.Task<bool> CanCloseShellAsync() {
        if (_settingsPage is { HasChanges: true }) {
            return await _settingsPage.CanLeaveAsync();
        }

        return true;
    }

    private PageViewModelBase EnsureShellPage(ShellPageId pageId) => pageId switch {
        ShellPageId.WeChat => WeChatPage,
        ShellPageId.Settings => SettingsPage,
        ShellPageId.About => AboutPage,
        _ => Workspace,
    };

    private SettingsViewModel CreateSettingsPage() {
        var page = new SettingsViewModel(this);
        page.Reload();
        return page;
    }

    partial void OnIsDarkThemeChanged(bool value) => RefreshFooterNavItems();

    partial void OnThemeModeChanged(AppThemeMode value) => RefreshFooterNavItems();

    partial void OnIsLoggedInChanged(bool value) => RefreshFooterNavItems();

    private void RefreshFooterNavItems() {
        ThemeFooterItem.NavIcon = IsDarkTheme ? Icon.WeatherSunny : Icon.WeatherMoon;
        ThemeFooterItem.ToolTip = ThemeMode == AppThemeMode.System
            ? (IsDarkTheme ? "切换到浅色（将退出跟随系统）" : "切换到深色（将退出跟随系统）")
            : (IsDarkTheme ? "切换到浅色" : "切换到深色");

        AccountFooterItem.NavIcon = IsLoggedIn ? Icon.SignOut : Icon.Person;
        AccountFooterItem.Title = IsLoggedIn ? "登出" : "登录";
        AccountFooterItem.ToolTip = AccountFooterItem.Title;
    }

    /// <summary>
    /// 应用并持久化外观偏好，同时同步侧栏与设置页。
    /// </summary>
    public void ApplyTheme(AppThemeMode mode) {
        PreviewTheme(mode);
        var settings = AppSettings.Instance;
        if (settings.ThemeMode != mode) {
            settings.ThemeMode = mode;
            settings.IsDarkTheme = ThemeModeHelper.ToLegacyIsDarkTheme(mode);
            settings.Save();
        }
        _settingsPage?.SyncThemeMode(mode);
    }

    public void PreviewTheme(AppThemeMode mode) {
        ThemeMode = mode;
        AppThemeService.Apply(mode);
        SyncEffectiveIsDark();
        foreach (var document in Workspace.Documents) document.RefreshPreviewForThemeChange();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) {
        SyncEffectiveIsDark();
        foreach (var document in Workspace.Documents) document.RefreshPreviewForThemeChange();
    }

    private void SyncEffectiveIsDark() {
        var isDark = AppThemeService.ResolveEffectiveIsDark(ThemeMode);
        if (IsDarkTheme != isDark) {
            IsDarkTheme = isDark;
        }
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

    private void OnActivePageChanged(PageViewModelBase? value) {
        OnPropertyChanged(nameof(IsWorkspaceActive));
        OnPropertyChanged(nameof(SecondaryPage));
        if (value is WeChatViewModel weChat) {
            weChat.SyncFrom(PublishPage);
        }
        else if (value is SettingsViewModel settings) {
            if (_restoreSettingsAfterModelCatalog) {
                _restoreSettingsAfterModelCatalog = false;
            }
            else if (!settings.HasChanges) {
                settings.Reload();
            }
        }

        RefreshChromeTitle();
    }

    public void NavigateTo(PageViewModelBase page) {
        if (page == Workspace || page == WeChatPage || page == SettingsPage || page == AboutPage) {
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
    private void ToggleTheme() {
        var next = NextThemeMode();
        if (ActivePage == SettingsPage || ActivePage is ModelCatalogPageViewModel)
            SettingsPage.ThemeMode = next;
        else ApplyTheme(next);
    }

    private AppThemeMode NextThemeMode() {
        if (ThemeMode == AppThemeMode.System)
            return IsDarkTheme ? AppThemeMode.Light : AppThemeMode.Dark;
        return ThemeMode == AppThemeMode.Light ? AppThemeMode.Dark : AppThemeMode.Light;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Login() {
        if (!AuthService.HasCredentials) {
            GuiHost.ToastWarning("登录", "请先在设置中配置用户名和密码");
            ActivePage = SettingsPage;
            SettingsPage.SelectedSection = 1;
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
