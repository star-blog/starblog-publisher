using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.AI;
using StarBlogPublisher.Services.Application;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

public partial class SettingsViewModel : PageViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly AIModelCatalogService _modelCatalogService = new();
    private string? _lastProviderName;

    public SettingsViewModel(MainWindowViewModel shell) : base("设置", Icon.Settings) {
        _shell = shell;
        Reload();
        Profiles.CollectionChanged += (_, _) => UpdateDirtyState();
        WeChatAccounts.CollectionChanged += (_, _) => UpdateDirtyState();
    }

    [ObservableProperty] private bool _useProxy;
    [ObservableProperty] private string _proxyType = "http";
    [ObservableProperty] private string _proxyHost = string.Empty;
    [ObservableProperty] private int _proxyPort;
    [ObservableProperty] private int _proxyTimeout;
    [ObservableProperty] private bool _useCustomBackend;
    [ObservableProperty] private string _backendUrl = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private int _backendTimeout;
    [ObservableProperty] private bool _showPassword;
    [ObservableProperty] private bool _enableRegexImageParsing;
    [ObservableProperty] private string _weChatAccountName = string.Empty;
    [ObservableProperty] private string _weChatAppId = string.Empty;
    [ObservableProperty] private string _weChatApiBaseUrl = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    [ObservableProperty] private string _weChatApiAuthorization = string.Empty;
    [ObservableProperty] private string _weChatAppSecret = string.Empty;
    [ObservableProperty] private string _weChatAuthor = string.Empty;
    [ObservableProperty] private string _weChatDefaultTheme = "newspaper";
    [ObservableProperty] private bool _showWeChatAppSecret;
    [ObservableProperty] private bool _showWeChatApiAuthorization;
    [ObservableProperty] private ObservableCollection<WeChatAccountProfile> _weChatAccounts = new();
    [ObservableProperty] private WeChatAccountProfile? _currentWeChatAccount;
    [ObservableProperty] private bool _isDarkTheme;
    private bool _syncingTheme;

    [ObservableProperty] private bool _enableAI;
    [ObservableProperty] private string _AIProvider = "openai";
    [ObservableProperty] private string _AIKey = string.Empty;
    [ObservableProperty] private string _AIModel = string.Empty;
    [ObservableProperty] private string _AIApiBase = string.Empty;
    [ObservableProperty] private bool _showAIKey;
    [ObservableProperty] private bool _isLoadingModels;
    [ObservableProperty] private Vector _settingsScrollOffset;
    [ObservableProperty] private ObservableCollection<AIModelDescriptor> _availableModels = new();
    [ObservableProperty] private string _aiStatusMessage = "准备就绪";
    [ObservableProperty] private string _aiModelDetails = "\u8bf7\u9009\u62e9\u6a21\u578b\u4ee5\u67e5\u770b\u76ee\u5f55\u8be6\u60c5\u3002";
    [ObservableProperty] private ObservableCollection<AIProfile> _profiles = new();
    [ObservableProperty] private AIProfile? _currentProfile;

    public IReadOnlyList<WeChatTheme> WeChatThemes { get; } = WeChatFormattingService.Themes;

    public List<AIProviderInfo> AIProviders { get; } = AIProviderInfo.GetProviders();

    public bool IsCustomProvider => AIProvider == "custom";

    public AIProviderInfo? SelectedAIProvider {
        get => AIProviders.FirstOrDefault(provider => provider.Name == AIProvider);
        set { if (value != null && value.Name != AIProvider) AIProvider = value.Name; }
    }

    private AIProviderInfo? _currentProviderInfo;

    public AIProviderInfo? CurrentProvider {
        get {
            if (_currentProviderInfo?.Name != AIProvider) {
                _currentProviderInfo = AIProviderInfo.GetProvider(AIProvider);
            }

            return _currentProviderInfo;
        }
    }

    public void Reload() {
        var settings = AppSettings.Instance;
        _loadingDraft = true;
        _syncingTheme = true;
        try {
            UseProxy = settings.UseProxy;
            ProxyType = settings.ProxyType;
            ProxyHost = settings.ProxyHost;
            ProxyPort = settings.ProxyPort;
            ProxyTimeout = settings.ProxyTimeout;
            // Legacy clients selected a backend by URL alone; show the service actually in use.
            UseCustomBackend = settings.UseCustomBackend || !string.IsNullOrWhiteSpace(settings.BackendUrl);
            BackendUrl = settings.BackendUrl;
            Username = settings.Username;
            Password = settings.Password;
            BackendTimeout = settings.BackendTimeout;
            EnableRegexImageParsing = settings.EnableRegexImageParsing;
            WeChatDefaultTheme = WeChatThemeCatalog.NormalizeId(settings.WeChatDefaultTheme);
            IsDarkTheme = settings.IsDarkTheme;
            LoadWeChatAccounts();
            LoadProfiles();
        }
        finally {
            _syncingTheme = false;
            _loadingDraft = false;
        }
        AcceptChanges();
    }

    /// <summary>
    /// 从壳层同步主题开关，避免再次走持久化。
    /// </summary>
    public void SyncDarkTheme(bool isDark) {
        if (IsDarkTheme == isDark) return;
        var wasDirty = HasChanges;
        _syncingTheme = true;
        try {
            IsDarkTheme = isDark;
        }
        finally {
            _syncingTheme = false;
        }
        if (!wasDirty) AcceptChanges();
    }

    private void LoadProfiles() {
        var settings = AppSettings.Instance;
        Profiles.Clear();
        foreach (var profile in settings.AIProfiles) {
            Profiles.Add(profile.Clone());
        }

        var currentProfileName = settings.CurrentAIProfile;
        CurrentProfile = Profiles.FirstOrDefault(p => p.Name == currentProfileName) ?? Profiles.FirstOrDefault();

        if (CurrentProfile == null && Profiles.Count == 0) {
            var defaultProfile = new AIProfile {
                Name = "默认",
                EnableAI = settings.EnableAI,
                Provider = settings.AIProvider,
                Key = settings.AIKey,
                Model = settings.AIModel,
                ApiBase = settings.AIApiBase
            };
            Profiles.Add(defaultProfile);
            CurrentProfile = defaultProfile;
        }
    }

    partial void OnCurrentProfileChanged(AIProfile? value) {
        if (value != null) LoadProfileSettings(value);
    }

    partial void OnCurrentProfileChanging(AIProfile? value) {
        if (!_loadingDraft) SaveProfileSettings();
    }

    partial void OnAIProviderChanged(string value) {
        var provider = AIProviders.FirstOrDefault(p => p.DisplayName == value || p.Name == value);
        if (provider != null && provider.Name != value) {
            AIProvider = provider.Name;
            return;
        }

        OnPropertyChanged(nameof(IsCustomProvider));
        OnPropertyChanged(nameof(SelectedAIProvider));
        if (CurrentProvider == null) return;
        var providerChanged = !string.Equals(_lastProviderName, CurrentProvider.Name, StringComparison.Ordinal);
        _lastProviderName = CurrentProvider.Name;
        if (!IsCustomProvider) {
            AIApiBase = CurrentProvider.DefaultApiBase;
            if (providerChanged || string.IsNullOrWhiteSpace(AIModel)) {
                AIModel = CurrentProvider.DefaultModel;
            }
        }

        ResetModelCatalog();
    }

    private void LoadProfileSettings(AIProfile profile) {
        var wasLoading = _loadingDraft;
        _loadingDraft = true;
        try {
            EnableAI = profile.EnableAI;
            AIProvider = profile.Provider;
            AIKey = profile.Key;
            AIModel = profile.Model;
            AIApiBase = profile.ApiBase;
            ResetModelCatalog();
        }
        finally { _loadingDraft = wasLoading; }
    }

    private void LoadWeChatAccounts() {
        var settings = AppSettings.Instance;
        WeChatAccounts.Clear();
        foreach (var account in settings.WeChatAccounts) {
            WeChatAccounts.Add(account.Clone());
        }

        CurrentWeChatAccount = WeChatAccounts.FirstOrDefault(account => account.Id == settings.CurrentWeChatAccountId)
            ?? WeChatAccounts.FirstOrDefault();
    }

    partial void OnCurrentWeChatAccountChanged(WeChatAccountProfile? value) {
        if (value != null) LoadWeChatAccountSettings(value);
    }

    partial void OnCurrentWeChatAccountChanging(WeChatAccountProfile? value) {
        if (!_loadingDraft) SaveWeChatAccountSettings();
    }

    private void LoadWeChatAccountSettings(WeChatAccountProfile account) {
        var wasLoading = _loadingDraft;
        _loadingDraft = true;
        try {
            WeChatAccountName = account.Name;
            WeChatAppId = account.AppId;
            WeChatApiBaseUrl = account.ApiBaseUrl;
            WeChatApiAuthorization = account.ApiAuthorization;
            WeChatAppSecret = account.AppSecret;
            WeChatAuthor = account.Author;
            UseWeChatRelay = !string.Equals(account.ApiBaseUrl.TrimEnd('/'), WeChatHttpClientRegistration.OfficialApiBaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(account.ApiAuthorization);
            _relayUrl = UseWeChatRelay ? account.ApiBaseUrl : string.Empty;
            _relayAuthorization = account.ApiAuthorization;
        }
        finally { _loadingDraft = wasLoading; }
    }

    private void SaveWeChatAccountSettings() {
        if (CurrentWeChatAccount == null) return;
        CurrentWeChatAccount.Name = WeChatAccountName;
        CurrentWeChatAccount.AppId = WeChatAppId;
        CurrentWeChatAccount.ApiBaseUrl = WeChatApiBaseUrl;
        CurrentWeChatAccount.ApiAuthorization = WeChatApiAuthorization;
        CurrentWeChatAccount.AppSecret = WeChatAppSecret;
        CurrentWeChatAccount.Author = WeChatAuthor;
    }

    partial void OnAIModelChanged(string value) => UpdateSelectedModelDetails();

    private void SaveProfileSettings() {
        if (CurrentProfile == null) return;
        CurrentProfile.EnableAI = EnableAI;
        CurrentProfile.Provider = AIProvider;
        CurrentProfile.Key = AIKey;
        CurrentProfile.Model = AIModel;
        CurrentProfile.ApiBase = AIApiBase;
    }

    [RelayCommand]
    private void TogglePassword() => ShowPassword = !ShowPassword;

    [RelayCommand]
    private void ToggleWeChatAppSecret() => ShowWeChatAppSecret = !ShowWeChatAppSecret;

    [RelayCommand]
    private void ToggleWeChatApiAuthorization() => ShowWeChatApiAuthorization = !ShowWeChatApiAuthorization;

    [RelayCommand]
    private async Task AddWeChatAccount() {
        var name = await GuiHost.PromptAsync("添加公众号账号", "新公众号", "请输入用于区分此公众号的名称");
        if (string.IsNullOrWhiteSpace(name)) return;

        var accountName = name.Trim();
        if (WeChatAccounts.Any(account => string.Equals(account.Name, accountName, StringComparison.Ordinal))) {
            GuiHost.ToastWarning("公众号账号", "已存在同名账号，请使用其他名称。");
            return;
        }

        SaveWeChatAccountSettings();
        var account = new WeChatAccountProfile { Name = accountName };
        WeChatAccounts.Add(account);
        CurrentWeChatAccount = account;
    }

    [RelayCommand]
    private async Task DeleteWeChatAccount() {
        if (CurrentWeChatAccount == null || WeChatAccounts.Count <= 1) {
            GuiHost.ToastWarning("公众号账号", "至少需要保留一个公众号账号。");
            return;
        }

        if (!await GuiHost.ConfirmAsync("删除公众号账号", $"确定要删除 \"{CurrentWeChatAccount.DisplayName}\" 吗？")) return;

        var index = WeChatAccounts.IndexOf(CurrentWeChatAccount);
        WeChatAccounts.Remove(CurrentWeChatAccount);
        CurrentWeChatAccount = WeChatAccounts[Math.Min(index, WeChatAccounts.Count - 1)];
    }

    [RelayCommand]
    private void ToggleAIKey() => ShowAIKey = !ShowAIKey;

    partial void OnIsDarkThemeChanged(bool value) {
        if (_syncingTheme) return;
        _shell.PreviewTheme(value);
    }

    private void ResetModelCatalog() {
        ++_catalogRequest;
        IsLoadingModels = false;
        AvailableModels.Clear();
        AiStatusMessage = "填写凭据后，点击“获取模型”加载目录。";
        UpdateSelectedModelDetails();
    }

    partial void OnAIKeyChanged(string value) => ResetModelCatalog();
    partial void OnAIApiBaseChanged(string value) => ResetModelCatalog();

    [RelayCommand]
    private async Task RefreshModels() {
        if (CurrentProvider == null) return;

        var request = ++_catalogRequest;
        var provider = CurrentProvider;
        IsLoadingModels = true;
        AiStatusMessage = "正在加载模型列表...";
        AvailableModels.Clear();

        var apiBase = IsCustomProvider ? AIApiBase : CurrentProvider.DefaultApiBase;
        var result = await _modelCatalogService.GetModelsAsync(provider, AIKey, apiBase);
        if (request != _catalogRequest) return;
        foreach (var model in result.Models) {
            AvailableModels.Add(model);
        }

        if (!string.IsNullOrEmpty(AIModel) && AvailableModels.All(model => !string.Equals(model.Id, AIModel, StringComparison.OrdinalIgnoreCase))) {
            AvailableModels.Add(new AIModelDescriptor(AIModel, Source: AIModelSource.SavedConfiguration));
        }

        AiStatusMessage = $"{provider.CapabilitySummary}. {result.Message}";
        UpdateSelectedModelDetails();
        IsLoadingModels = false;
    }

    private void UpdateSelectedModelDetails() {
        var model = AvailableModels.FirstOrDefault(item => string.Equals(item.Id, AIModel, StringComparison.OrdinalIgnoreCase));
        if (model == null) {
            AiModelDetails = string.IsNullOrWhiteSpace(AIModel)
                ? "\u8bf7\u9009\u62e9\u6a21\u578b\u4ee5\u67e5\u770b\u76ee\u5f55\u8be6\u60c5\u3002"
                : $"{AIModel}\uff1a\u5df2\u4fdd\u5b58\u7684\u6a21\u578b\uff0c\u5c1a\u672a\u5728\u63d0\u4f9b\u5546\u76ee\u5f55\u4e2d\u9a8c\u8bc1\u3002";
            return;
        }

        var source = model.Source switch {
            AIModelSource.Provider => "\u63d0\u4f9b\u5546\u76ee\u5f55",
            AIModelSource.Recommended => "\u5185\u7f6e\u63a8\u8350",
            _ => "\u5df2\u4fdd\u5b58\u7684\u914d\u7f6e\uff08\u5c1a\u672a\u901a\u8fc7\u63d0\u4f9b\u5546\u76ee\u5f55\u9a8c\u8bc1\uff09"
        };
        var context = model.ContextLength is null ? "\u4e0a\u4e0b\u6587\u7a97\u53e3\u672a\u77e5" : $"{model.ContextLength:N0} tokens \u4e0a\u4e0b\u6587\u7a97\u53e3";
        AiModelDetails = $"{source}; {context}; {model.PriceSummary}.";
    }

    [RelayCommand]
    private async Task AddProfile() {
        var name = await GuiHost.PromptAsync("添加配置方案", "新配置", "请输入配置方案名称");
        if (string.IsNullOrWhiteSpace(name)) return;

        var profileName = name.Trim();
        if (Profiles.Any(p => p.Name == profileName)) {
            AiStatusMessage = "已存在同名配置方案，请使用其他名称";
            GuiHost.ToastWarning("配置方案", AiStatusMessage);
            return;
        }

        SaveProfileSettings();
        var newProfile = new AIProfile {
            Name = profileName,
            EnableAI = EnableAI,
            Provider = AIProvider,
            Key = AIKey,
            Model = AIModel,
            ApiBase = AIApiBase
        };
        Profiles.Add(newProfile);
        CurrentProfile = newProfile;
        AiStatusMessage = $"已添加配置方案 \"{profileName}\"";
    }

    [RelayCommand]
    private async Task DeleteProfile() {
        if (CurrentProfile == null) return;
        if (Profiles.Count <= 1) {
            AiStatusMessage = "至少需要保留一个配置方案";
            GuiHost.ToastWarning("配置方案", AiStatusMessage);
            return;
        }

        if (!await GuiHost.ConfirmAsync("删除配置方案", $"确定要删除配置方案 \"{CurrentProfile.Name}\" 吗？")) {
            return;
        }

        var profileName = CurrentProfile.Name;
        var index = Profiles.IndexOf(CurrentProfile);
        Profiles.Remove(CurrentProfile);
        if (Profiles.Count > 0) {
            CurrentProfile = Profiles[Math.Min(index, Profiles.Count - 1)];
        }

        AiStatusMessage = $"已删除配置方案 \"{profileName}\"";
    }

    [RelayCommand]
    private async Task RenameProfile() {
        if (CurrentProfile == null) return;
        var newName = await GuiHost.PromptAsync("重命名配置方案", CurrentProfile.Name, "请输入新的配置方案名称");
        if (string.IsNullOrWhiteSpace(newName)) return;

        newName = newName.Trim();
        if (newName != CurrentProfile.Name && Profiles.Any(p => p.Name == newName)) {
            AiStatusMessage = "已存在同名配置方案，请使用其他名称";
            GuiHost.ToastWarning("配置方案", AiStatusMessage);
            return;
        }

        var oldName = CurrentProfile.Name;
        CurrentProfile.Name = newName;
        OnPropertyChanged(nameof(Profiles));
        UpdateDirtyState();
        AiStatusMessage = $"已将配置方案 \"{oldName}\" 重命名为 \"{newName}\"";
    }

    [RelayCommand]
    private Task OpenModelCatalog() {
        SelectedSection = 3;
        return _shell.OpenModelCatalogAsync();
    }

    internal async Task<ModelCatalogSnapshot> RefreshModelCatalogAsync() {
        await RefreshModels();
        return new ModelCatalogSnapshot(AvailableModels.ToArray(), AiStatusMessage);
    }

    internal void SelectModel(string modelId) => AIModel = modelId;

    [RelayCommand]
    private void Save() {
        SaveProfileSettings();
        SaveWeChatAccountSettings();
        if (!ValidateDraft()) return;
        if (!AppSettings.Instance.TryUpdate(ApplyDraft, out var error)) {
            ValidationMessage = error ?? "保存失败，请重试。";
            return;
        }
        AcceptChanges();
        _shell.ApplyTheme(IsDarkTheme);
        _shell.PublishPage.NotifyAiEnabled();
        foreach (var document in _shell.Workspace.Documents) document.NotifyAiEnabled();
        GuiHost.ToastSuccess("设置", "已保存");
    }

    private void ApplyDraft(AppSettings settings) {
        WeChatHttpClientRegistration.TryGetApiBaseAddress(WeChatApiBaseUrl, out var weChatApiBaseAddress);

        settings.UseProxy = UseProxy;
        settings.ProxyType = ProxyType;
        settings.ProxyHost = ProxyHost;
        settings.ProxyPort = ProxyPort;
        settings.ProxyTimeout = ProxyTimeout;
        settings.UseCustomBackend = UseCustomBackend;
        settings.BackendUrl = UseCustomBackend ? BackendUrl.Trim() : string.Empty;
        settings.Username = Username;
        settings.Password = Password;
        settings.BackendTimeout = BackendTimeout;
        settings.EnableRegexImageParsing = EnableRegexImageParsing;
        settings.WeChatAppId = WeChatAppId;
        settings.WeChatApiBaseUrl = weChatApiBaseAddress.AbsoluteUri;
        settings.WeChatApiAuthorization = WeChatApiAuthorization;
        settings.WeChatAppSecret = WeChatAppSecret;
        settings.WeChatAuthor = WeChatAuthor;
        settings.WeChatAccounts = WeChatAccounts.Select(account => account.Clone()).ToList();
        settings.CurrentWeChatAccountId = CurrentWeChatAccount?.Id ?? settings.WeChatAccounts[0].Id;
        settings.WeChatDefaultTheme = WeChatDefaultTheme;
        settings.IsDarkTheme = IsDarkTheme;

        settings.EnableAI = EnableAI;
        settings.AIProvider = AIProvider;
        settings.AIKey = AIKey;
        settings.AIModel = AIModel;
        settings.AIApiBase = AIApiBase;
        settings.AIProfiles.Clear();
        foreach (var profile in Profiles) {
            settings.AIProfiles.Add(profile.Clone());
        }

        if (CurrentProfile != null) {
            settings.CurrentAIProfile = CurrentProfile.Name;
        }

    }

    [RelayCommand]
    private void Cancel() {
        Reload();
        _shell.PreviewTheme(IsDarkTheme);
        GuiHost.ToastInfo("设置", "已还原为上次保存的配置");
    }
}
