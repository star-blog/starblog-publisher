using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class SettingsViewModel {
    private bool _loadingDraft;
    private string _savedFingerprint = string.Empty;
    private int _catalogRequest;
    private int _validationSection = -1;
    private string? _validationField;

    [ObservableProperty] private int _selectedSection;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private string _validationMessage = string.Empty;
    [ObservableProperty] private bool _isWeChatAdvancedExpanded;
    [ObservableProperty] private bool _useWeChatRelay;
    private string _relayUrl = string.Empty;
    private string _relayAuthorization = string.Empty;

    public bool IsGeneralSection => SelectedSection == 0;
    public bool IsBlogSection => SelectedSection == 1;
    public bool IsWeChatSection => SelectedSection == 2;
    public bool IsAiSection => SelectedSection == 3;
    public bool IsProxySection => SelectedSection == 4;
    public bool IsBackupSection => SelectedSection == 5;
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);
    public string BlogNavigationLabel => NavigationLabel("博客连接", 1);
    public string WeChatNavigationLabel => NavigationLabel("微信公众号", 2);
    public string AiNavigationLabel => NavigationLabel("AI 创作", 3);
    public string ProxyNavigationLabel => NavigationLabel("网络代理", 4);
    private string NavigationLabel(string label, int section) => HasValidationError && _validationSection == section ? label + " · 待修正" : label;
    public string[]? BackendErrors => FieldErrors(nameof(BackendUrl));
    public string[]? AccountNameErrors => FieldErrors(nameof(WeChatAccountName));
    public string[]? RelayUrlErrors => FieldErrors(nameof(WeChatApiBaseUrl));
    public string[]? RelayAuthErrors => FieldErrors(nameof(WeChatApiAuthorization));
    public string[]? AiUrlErrors => FieldErrors(nameof(AIApiBase));
    private string[]? FieldErrors(string field) => HasValidationError && _validationField == field ? [ValidationMessage] : null;
    public string SaveStatus => HasChanges ? "有未保存的更改 · 保存应用于所有分类" : "没有未保存的更改";

    partial void OnSelectedSectionChanged(int value) {
        OnPropertyChanged(nameof(IsGeneralSection));
        OnPropertyChanged(nameof(IsBlogSection));
        OnPropertyChanged(nameof(IsWeChatSection));
        OnPropertyChanged(nameof(IsAiSection));
        OnPropertyChanged(nameof(IsProxySection));
        OnPropertyChanged(nameof(IsBackupSection));
        SettingsScrollOffset = default;
    }

    partial void OnHasChangesChanged(bool value) => OnPropertyChanged(nameof(SaveStatus));
    partial void OnValidationMessageChanged(string value) {
        foreach (var property in new[] { nameof(HasValidationError), nameof(BlogNavigationLabel), nameof(WeChatNavigationLabel),
            nameof(AiNavigationLabel), nameof(ProxyNavigationLabel), nameof(BackendErrors), nameof(AccountNameErrors),
            nameof(RelayUrlErrors), nameof(RelayAuthErrors), nameof(AiUrlErrors) }) OnPropertyChanged(property);
    }

    partial void OnUseWeChatRelayChanged(bool value) {
        if (_loadingDraft) return;
        if (value) {
            WeChatApiBaseUrl = _relayUrl;
            WeChatApiAuthorization = _relayAuthorization;
        }
        else {
            _relayUrl = WeChatApiBaseUrl;
            _relayAuthorization = WeChatApiAuthorization;
            WeChatApiBaseUrl = WeChatHttpClientRegistration.OfficialApiBaseUrl;
            WeChatApiAuthorization = string.Empty;
        }
    }

    private static readonly HashSet<string> DraftProperties = [
        nameof(UseProxy), nameof(ProxyType), nameof(ProxyHost), nameof(ProxyPort), nameof(ProxyTimeout),
        nameof(UseCustomBackend), nameof(BackendUrl), nameof(Username), nameof(Password), nameof(BackendTimeout),
        nameof(EnableRegexImageParsing), nameof(ThemeMode), nameof(WeChatDefaultTheme),
        nameof(WeChatAccountName), nameof(WeChatAppId), nameof(WeChatApiBaseUrl), nameof(WeChatApiAuthorization),
        nameof(WeChatAppSecret), nameof(WeChatAuthor), nameof(CurrentWeChatAccount),
        nameof(EnableAI), nameof(AIProvider), nameof(AIKey), nameof(AIModel), nameof(AIApiBase), nameof(CurrentProfile)
    ];

    protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
        base.OnPropertyChanged(e);
        if (!_loadingDraft && e.PropertyName != null && DraftProperties.Contains(e.PropertyName)) {
            UpdateDirtyState();
            if (e.PropertyName == _validationField || e.PropertyName is nameof(CurrentWeChatAccount) or nameof(CurrentProfile))
                ValidationMessage = string.Empty;
        }
    }

    private void UpdateDirtyState() {
        if (_loadingDraft) return;
        SaveProfileSettings();
        SaveWeChatAccountSettings();
        HasChanges = Fingerprint() != _savedFingerprint;
    }

    // Length-prefixed values avoid delimiter collisions; only a digest of credentials is retained.
    private string Fingerprint() {
        var values = new List<string> {
            UseProxy.ToString(), ProxyType, ProxyHost, ProxyPort.ToString(), ProxyTimeout.ToString(),
            UseCustomBackend.ToString(), BackendUrl, Username, Password, BackendTimeout.ToString(),
            EnableRegexImageParsing.ToString(), ThemeMode.ToString(), WeChatDefaultTheme,
            CurrentWeChatAccount?.Id ?? "", CurrentProfile?.Name ?? ""
        };
        foreach (var account in WeChatAccounts) values.AddRange([
            account.Id, account.Name, account.AppId, account.AppSecret, account.ApiBaseUrl, account.ApiAuthorization, account.Author]);
        foreach (var profile in Profiles) values.AddRange([
            profile.Name, profile.EnableAI.ToString(), profile.Provider, profile.Key, profile.Model, profile.ApiBase]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(values.Select(v => $"{v?.Length ?? 0}:{v}")))));
    }

    private void AcceptChanges() {
        SaveProfileSettings();
        SaveWeChatAccountSettings();
        _savedFingerprint = Fingerprint();
        HasChanges = false;
        ValidationMessage = string.Empty;
    }

    private bool Invalid(int section, string message, string? field = null) {
        SelectedSection = section;
        _validationSection = section;
        _validationField = field;
        ValidationMessage = message;
        SettingsScrollOffset = default;
        return false;
    }

    private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https") && !string.IsNullOrWhiteSpace(uri.Host);

    private bool ValidateDraft() {
        ValidationMessage = string.Empty;
        if (UseCustomBackend && !IsHttpUrl(BackendUrl)) return Invalid(1, "服务 URL：请输入完整的 HTTP 或 HTTPS 地址。", nameof(BackendUrl));
        if (BackendTimeout is < 1 or > 600) return Invalid(1, "请求超时：请输入 1 至 600 秒。");
        if (UseProxy && (string.IsNullOrWhiteSpace(ProxyHost) || ProxyPort is < 1 or > 65535 || ProxyTimeout is < 1 or > 600))
            return Invalid(4, "请填写代理主机名、有效端口（1–65535）和超时（1–600 秒）。");
        foreach (var account in WeChatAccounts) {
            string? error = string.IsNullOrWhiteSpace(account.Name) ? "请填写账号名称。"
                : WeChatAccounts.Count(a => a.Name.Trim() == account.Name.Trim()) > 1 ? "账号名称不能重复。"
                : !WeChatHttpClientRegistration.TryGetApiBaseAddress(account.ApiBaseUrl, out _) ? "请输入完整的 HTTP 或 HTTPS 中转地址。"
                : !WeChatHttpClientRegistration.IsValidApiAuthorization(account.ApiAuthorization) ? "请填写完整的认证值，例如 Bearer relay-token。" : null;
            if (error == null) continue;
            CurrentWeChatAccount = account;
            IsWeChatAdvancedExpanded = true;
            var field = string.IsNullOrWhiteSpace(account.Name) || WeChatAccounts.Count(a => a.Name.Trim() == account.Name.Trim()) > 1
                ? nameof(WeChatAccountName)
                : !WeChatHttpClientRegistration.TryGetApiBaseAddress(account.ApiBaseUrl, out _) ? nameof(WeChatApiBaseUrl) : nameof(WeChatApiAuthorization);
            return Invalid(2, $"账号“{account.DisplayName}”：{error}", field);
        }
        foreach (var profile in Profiles) {
            if (profile.EnableAI && profile.Provider == "custom" && !IsHttpUrl(profile.ApiBase)) {
                CurrentProfile = profile;
                return Invalid(3, $"方案“{profile.Name}”：请输入完整的 API Base URL。", nameof(AIApiBase));
            }
        }
        return true;
    }

    public enum LeaveChoice { Cancel, Save, Discard }
    public Func<Task<LeaveChoice>>? ConfirmLeaveOverride { get; set; }

    public async Task<bool> CanLeaveAsync() {
        if (!HasChanges) return true;
        LeaveChoice choice;
        if (ConfirmLeaveOverride != null) choice = await ConfirmLeaveOverride();
        else {
            var dialog = new FAContentDialog {
                Title = "保存设置更改？", Content = "你有尚未保存的配置。保存将应用所有分类的更改。",
                PrimaryButtonText = "保存更改", SecondaryButtonText = "放弃更改", CloseButtonText = "继续编辑",
                DefaultButton = FAContentDialogButton.Primary
            };
            choice = await dialog.ShowAsync(GuiHost.GetMainWindow()) switch {
                FAContentDialogResult.Primary => LeaveChoice.Save,
                FAContentDialogResult.Secondary => LeaveChoice.Discard,
                _ => LeaveChoice.Cancel
            };
        }
        if (choice == LeaveChoice.Save) { Save(); return !HasChanges; }
        if (choice == LeaveChoice.Discard) { Cancel(); return true; }
        return false;
    }
}
