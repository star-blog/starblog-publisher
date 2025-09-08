using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase {
    private bool _useProxy;
    private string _proxyType = "http";
    private string _proxyHost = string.Empty;
    private int _proxyPort;
    private int _proxyTimeout;
    private bool _useCustomBackend;
    private string _backendUrl = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private int _backendTimeout;
    private bool _showPassword;
    private bool _enableRegexImageParsing;

    public bool UseProxy {
        get => _useProxy;
        set => SetProperty(ref _useProxy, value);
    }

    public string ProxyType {
        get => _proxyType;
        set => SetProperty(ref _proxyType, value);
    }

    public string ProxyHost {
        get => _proxyHost;
        set => SetProperty(ref _proxyHost, value);
    }

    public int ProxyPort {
        get => _proxyPort;
        set => SetProperty(ref _proxyPort, value);
    }

    public int ProxyTimeout {
        get => _proxyTimeout;
        set => SetProperty(ref _proxyTimeout, value);
    }

    public bool UseCustomBackend {
        get => _useCustomBackend;
        set => SetProperty(ref _useCustomBackend, value);
    }

    public string BackendUrl {
        get => _backendUrl;
        set => SetProperty(ref _backendUrl, value);
    }

    public string Username {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public int BackendTimeout {
        get => _backendTimeout;
        set => SetProperty(ref _backendTimeout, value);
    }

    public bool ShowPassword {
        get => _showPassword;
        set => SetProperty(ref _showPassword, value);
    }

    /// <summary>
    /// 是否启用正则表达式方式识别图片路径
    /// </summary>
    public bool EnableRegexImageParsing {
        get => _enableRegexImageParsing;
        set => SetProperty(ref _enableRegexImageParsing, value);
    }

    [RelayCommand]
    private void TogglePassword() {
        ShowPassword = !ShowPassword;
    }

    public SettingsWindowViewModel() {
        // 加载当前配置
        var settings = AppSettings.Instance;
        LoadSettings(settings);
    }

    private void LoadSettings(AppSettings settings) {
        UseProxy = settings.UseProxy;
        ProxyType = settings.ProxyType;
        ProxyHost = settings.ProxyHost;
        ProxyPort = settings.ProxyPort;
        ProxyTimeout = settings.ProxyTimeout;
        UseCustomBackend = settings.UseCustomBackend;
        BackendUrl = settings.BackendUrl;
        Username = settings.Username;
        Password = settings.Password;
        BackendTimeout = settings.BackendTimeout;
        EnableRegexImageParsing = settings.EnableRegexImageParsing;
    }

    [RelayCommand]
    private void Save() {
        var settings = AppSettings.Instance;

        settings.UseProxy = UseProxy;
        settings.ProxyType = ProxyType;
        settings.ProxyHost = ProxyHost;
        settings.ProxyPort = ProxyPort;
        settings.ProxyTimeout = ProxyTimeout;
        settings.UseCustomBackend = UseCustomBackend;
        settings.BackendUrl = BackendUrl;
        settings.Username = Username;
        settings.Password = Password;
        settings.BackendTimeout = BackendTimeout;
        settings.EnableRegexImageParsing = EnableRegexImageParsing;

        settings.Save();
        CloseWindow();
    }

    [RelayCommand]
    private void Cancel() {
        CloseWindow();
    }
    
    [RelayCommand]
    private void OpenAiSettings() {
        if (View is Window ownerWindow) {
            var aiSettingsWindow = new AiSettingsWindow();
            aiSettingsWindow.ShowDialog(ownerWindow);
        }
    }

    private void CloseWindow() {
        if (View is Window window) {
            window.Close();
        }
    }
}