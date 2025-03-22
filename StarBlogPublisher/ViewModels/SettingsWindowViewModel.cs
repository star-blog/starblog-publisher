using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase {
    private bool _useProxy;
    private string _proxyUrl = string.Empty;
    private int _proxyTimeout;
    private bool _useCustomBackend;
    private string _backendUrl = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private int _backendTimeout;
    private bool _showPassword;

    public bool UseProxy {
        get => _useProxy;
        set => SetProperty(ref _useProxy, value);
    }

    public string ProxyUrl {
        get => _proxyUrl;
        set => SetProperty(ref _proxyUrl, value);
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
        ProxyUrl = settings.ProxyUrl;
        ProxyTimeout = settings.ProxyTimeout;
        UseCustomBackend = settings.UseCustomBackend;
        BackendUrl = settings.BackendUrl;
        Username = settings.Username;
        Password = settings.Password;
        BackendTimeout = settings.BackendTimeout;
    }

    [RelayCommand]
    private void Save() {
        var settings = AppSettings.Instance;

        settings.UseProxy = UseProxy;
        settings.ProxyUrl = ProxyUrl;
        settings.ProxyTimeout = ProxyTimeout;
        settings.UseCustomBackend = UseCustomBackend;
        settings.BackendUrl = BackendUrl;
        settings.Username = Username;
        settings.Password = Password;
        settings.BackendTimeout = BackendTimeout;

        settings.Save();
        CloseWindow();
    }

    [RelayCommand]
    private void Cancel() {
        CloseWindow();
    }

    private void CloseWindow() {
        if (View is Window window) {
            window.Close();
        }
    }
}