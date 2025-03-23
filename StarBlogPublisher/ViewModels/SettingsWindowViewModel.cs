using System.Collections.Generic;
using System.Linq;
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
    private bool _showAIKey;
    private bool _enableAI;
    private string _aiProvider = "openai";
    private string _aiKey = string.Empty;
    private string _aiModel = string.Empty;
    private string _aiApiBase = string.Empty;
    private List<AIProviderInfo> _aiProviders = AIProviderInfo.GetProviders();

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

    public bool ShowAIKey {
        get => _showAIKey;
        set => SetProperty(ref _showAIKey, value);
    }

    public bool EnableAI {
        get => _enableAI;
        set => SetProperty(ref _enableAI, value);
    }

    public string AIProvider {
        get => _aiProvider;
        set {
            var provider = AIProviders.FirstOrDefault(p => p.DisplayName == value);
            var providerName = provider?.Name ?? value;
            if (SetProperty(ref _aiProvider, providerName)) {
                OnPropertyChanged(nameof(IsCustomProvider));
                OnAIProviderChanged(providerName);
            }
        }
    }
    
    public List<AIProviderInfo> AIProviders {
        get => _aiProviders;
    }
    
    public bool IsCustomProvider => AIProvider == "自定义";

    private AIProviderInfo? _currentProvider;
    public AIProviderInfo? CurrentProvider {
        get {
            if (_currentProvider?.Name != AIProvider) {
                _currentProvider = AIProviderInfo.GetProvider(AIProvider);
            }
            return _currentProvider;
        }
    }

    public string AIKey {
        get => _aiKey;
        set => SetProperty(ref _aiKey, value);
    }

    public string AIModel {
        get => _aiModel;
        set => SetProperty(ref _aiModel, value);
    }

    public string AIApiBase {
        get => _aiApiBase;
        set => SetProperty(ref _aiApiBase, value);
    }

    private void OnAIProviderChanged(string value) {
        if (!IsCustomProvider && CurrentProvider != null) {
            AIApiBase = CurrentProvider.DefaultApiBase;
        }
    }

    [RelayCommand]
    private void TogglePassword() {
        ShowPassword = !ShowPassword;
    }

    [RelayCommand]
    private void ToggleAIKey() {
        ShowAIKey = !ShowAIKey;
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
        EnableAI = settings.EnableAI;
        AIProvider = settings.AIProvider;
        AIKey = settings.AIKey;
        AIModel = settings.AIModel;
        AIApiBase = settings.AIApiBase;
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
        settings.EnableAI = EnableAI;
        settings.AIProvider = AIProvider;
        settings.AIKey = AIKey;
        settings.AIModel = AIModel;
        settings.AIApiBase = AIApiBase;

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