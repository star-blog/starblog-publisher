using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 应用壳：导航、主题、登录态、全局 Toast/Dialog。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase {
    internal readonly AuthApplicationService AuthService = new(
        AppSettings.Instance, GlobalState.Instance, ApiService.Instance
    );

    public ISukiToastManager ToastManager { get; } = GuiHost.Toasts;
    public ISukiDialogManager DialogManager { get; } = GuiHost.Dialogs;

    public PublishViewModel PublishPage { get; }
    public WeChatViewModel WeChatPage { get; }
    public SettingsViewModel SettingsPage { get; }
    public AboutViewModel AboutPage { get; }

    public IReadOnlyList<PageViewModelBase> Pages { get; }

    [ObservableProperty] private PageViewModelBase? _activePage;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _hasCredentials;
    [ObservableProperty] private string _loginStatusMessage = "未登录";
    [ObservableProperty] private string _softwareVersion = ApplicationVersion.Value;

    public MainWindowViewModel() {
        PublishPage = new PublishViewModel(this);
        WeChatPage = new WeChatViewModel();
        SettingsPage = new SettingsViewModel(this);
        AboutPage = new AboutViewModel();
        Pages = [PublishPage, WeChatPage, SettingsPage, AboutPage];
        ActivePage = PublishPage;

        GlobalState.Instance.StateChanged += OnGlobalStateChanged;
        UpdateLoginState();

        if (AuthService.HasCredentials) {
            _ = Login();
        }

        IsDarkTheme = AppSettings.Instance.IsDarkTheme;
        var theme = SukiTheme.GetInstance();
        theme.OnBaseThemeChanged += variant => IsDarkTheme = variant == ThemeVariant.Dark;
    }

    partial void OnActivePageChanged(PageViewModelBase? value) {
        if (value is WeChatViewModel weChat) {
            weChat.SyncFrom(PublishPage);
        }
        else if (value is SettingsViewModel settings) {
            settings.Reload();
        }
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

    [RelayCommand]
    private void ToggleTheme() {
        IsDarkTheme = !IsDarkTheme;
        SukiTheme.GetInstance().ChangeBaseTheme(IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light);
        AppSettings.Instance.IsDarkTheme = IsDarkTheme;
        AppSettings.Instance.Save();
    }

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
        PublishPage.NotifyLoginState(IsLoggedIn);

        if (IsLoggedIn && !wasLoggedIn) {
            PublishPage.RefreshCategoriesCommand.Execute(null);
        }
    }
}
