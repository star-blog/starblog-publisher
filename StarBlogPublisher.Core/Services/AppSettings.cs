using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarBlogPublisher.Services.Security;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

public class AppSettings {
    private const string ConfigPathOverrideEnvironmentVariable = "STARBLOGPUBLISHER_SETTINGS_PATH";
    private static string ConfigPath => ResolveConfigPath();

    private static AppSettings? _instance;
    private static string? _loadErrorMessage;

    public static string SettingsFilePath => ResolveConfigPath();
    public static string? LoadErrorMessage => _loadErrorMessage;
    public static bool HasLoadError => !string.IsNullOrWhiteSpace(_loadErrorMessage);

    public static AppSettings Instance {
        get {
            _instance ??= Load();
            return _instance;
        }
    }

    // 代理设置
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "http";
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; } = 0;
    public int ProxyTimeout { get; set; } = 30;

    // StarBlog后端设置
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;

    // AI设置
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    private string _encryptedAIKey = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string AIKey {
        get => EncryptionService.Decrypt(_encryptedAIKey);
        set => _encryptedAIKey = EncryptionService.Encrypt(value);
    }

    public string EncryptedAIKey {
        get => _encryptedAIKey;
        set => _encryptedAIKey = value;
    }

    public string AIModel { get; set; } = string.Empty;
    public string AIApiBase { get; set; } = string.Empty;

    // AI配置文件
    public List<AIProfile> AIProfiles { get; set; } = new List<AIProfile>();
    public string CurrentAIProfile { get; set; } = "默认";

    public string Username { get; set; } = string.Empty;

    // 用于存储加密后的密码
    private string _encryptedPassword = string.Empty;

    // 公开属性，读取时解密，设置时不做处理
    [System.Text.Json.Serialization.JsonIgnore]
    public string Password {
        get => EncryptionService.Decrypt(_encryptedPassword);
        set => _encryptedPassword = EncryptionService.Encrypt(value);
    }

    // 用于JSON序列化的属性
    public string EncryptedPassword {
        get => _encryptedPassword;
        set => _encryptedPassword = value;
    }

    public int BackendTimeout { get; set; } = 30;

    // 微信公众号设置
    public string WeChatAppId { get; set; } = string.Empty;
    public string WeChatApiBaseUrl { get; set; } = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    private string _encryptedWeChatApiAuthorization = string.Empty;
    private string _encryptedWeChatAppSecret = string.Empty;

    [JsonIgnore]
    public string WeChatApiAuthorization {
        get => EncryptionService.Decrypt(_encryptedWeChatApiAuthorization);
        set => _encryptedWeChatApiAuthorization = EncryptionService.Encrypt(value);
    }

    public string EncryptedWeChatApiAuthorization {
        get => _encryptedWeChatApiAuthorization;
        set => _encryptedWeChatApiAuthorization = value;
    }

    [JsonIgnore]
    public string WeChatAppSecret {
        get => EncryptionService.Decrypt(_encryptedWeChatAppSecret);
        set => _encryptedWeChatAppSecret = EncryptionService.Encrypt(value);
    }

    public string EncryptedWeChatAppSecret {
        get => _encryptedWeChatAppSecret;
        set => _encryptedWeChatAppSecret = value;
    }

    public string WeChatAuthor { get; set; } = string.Empty;
    public List<WeChatAccountProfile> WeChatAccounts { get; set; } = new();
    public string CurrentWeChatAccountId { get; set; } = string.Empty;
    public string WeChatDefaultTheme { get; set; } = "newspaper";

    /// <summary>Returns the active account, creating a migrated default profile when needed.</summary>
    public WeChatAccountProfile CurrentWeChatAccount {
        get {
            EnsureWeChatAccounts();
            return WeChatAccounts.First(profile => profile.Id == CurrentWeChatAccountId);
        }
    }

    // 主题设置
    public bool IsDarkTheme { get; set; } = false;

    // 编辑器
    public double EditorFontSize { get; set; } = 14;
    public bool EditorWordWrap { get; set; } = true;
    public bool EditorShowLineNumbers { get; set; }

    // 图片解析设置
    /// <summary>
    /// 是否启用正则表达式方式识别图片路径（用于处理带空格的图片路径）
    /// </summary>
    public bool EnableRegexImageParsing { get; set; } = false;

    // 配置变更事件
    public event EventHandler? SettingsChanged;

    [System.Text.Json.Serialization.JsonConstructor]
    internal AppSettings() { }

    private static AppSettings Load() {
        _loadErrorMessage = null;

        try {
            if (File.Exists(ConfigPath)) {
                var json = File.ReadAllText(ConfigPath);
                var snapshot = DeserializeSnapshot(json);

                if (snapshot != null) {
                    var settings = FromSnapshot(snapshot);

                    // 确保至少有一个默认配置文件
                    if (settings.AIProfiles == null || settings.AIProfiles.Count == 0) {
                        settings.MigrateToProfiles();
                    }
                    settings.EnsureWeChatAccounts();

                    return settings;
                }
            }
        }
        catch (Exception ex) {
            _loadErrorMessage = BuildLoadErrorMessage(ex);
            Trace.TraceWarning(_loadErrorMessage);
        }

        var defaultSettings = new AppSettings();
        defaultSettings.MigrateToProfiles();
        defaultSettings.EnsureWeChatAccounts();
        return defaultSettings;
    }

    internal static AppSettingsSnapshot DeserializeSnapshot(string json) {
        var legacySnapshot = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.LegacyAppSettingsSnapshot);
        if (legacySnapshot == null) {
            throw new JsonException("配置文件为空，或无法解析为已知格式。");
        }

        return ((LegacyAppSettingsSnapshot)legacySnapshot).ToAppSettingsSnapshot();
    }

    private static AppSettings FromSnapshot(AppSettingsSnapshot snapshot) {
        return new AppSettings {
            UseProxy = snapshot.UseProxy,
            ProxyType = snapshot.ProxyType ?? "http",
            ProxyHost = snapshot.ProxyHost ?? string.Empty,
            ProxyPort = snapshot.ProxyPort,
            ProxyTimeout = snapshot.ProxyTimeout,
            UseCustomBackend = snapshot.UseCustomBackend,
            BackendUrl = snapshot.BackendUrl ?? string.Empty,
            EnableAI = snapshot.EnableAI,
            AIProvider = snapshot.AIProvider ?? "openai",
            _encryptedAIKey = snapshot.EncryptedAIKey ?? string.Empty,
            AIModel = snapshot.AIModel ?? string.Empty,
            AIApiBase = snapshot.AIApiBase ?? string.Empty,
            AIProfiles = snapshot.AIProfiles ?? new List<AIProfile>(),
            CurrentAIProfile = snapshot.CurrentAIProfile ?? "默认",
            Username = snapshot.Username ?? string.Empty,
            _encryptedPassword = snapshot.EncryptedPassword ?? string.Empty,
            BackendTimeout = snapshot.BackendTimeout,
            WeChatAppId = snapshot.WeChatAppId ?? string.Empty,
            WeChatApiBaseUrl = string.IsNullOrWhiteSpace(snapshot.WeChatApiBaseUrl)
                ? WeChatHttpClientRegistration.OfficialApiBaseUrl
                : snapshot.WeChatApiBaseUrl,
            _encryptedWeChatApiAuthorization = snapshot.EncryptedWeChatApiAuthorization ?? string.Empty,
            _encryptedWeChatAppSecret = snapshot.EncryptedWeChatAppSecret ?? string.Empty,
            WeChatAuthor = snapshot.WeChatAuthor ?? string.Empty,
            WeChatAccounts = snapshot.WeChatAccounts ?? new List<WeChatAccountProfile>(),
            CurrentWeChatAccountId = snapshot.CurrentWeChatAccountId ?? string.Empty,
            WeChatDefaultTheme = snapshot.WeChatDefaultTheme ?? "newspaper",
            IsDarkTheme = snapshot.IsDarkTheme,
            EnableRegexImageParsing = snapshot.EnableRegexImageParsing,
            EditorFontSize = snapshot.EditorFontSize is >= 11 and <= 22 ? snapshot.EditorFontSize : 14,
            EditorWordWrap = snapshot.EditorWordWrap,
            EditorShowLineNumbers = snapshot.EditorShowLineNumbers
        };
    }

    private AppSettingsSnapshot ToSnapshot() {
        var currentWeChatAccount = CurrentWeChatAccount;
        return new AppSettingsSnapshot {
            UseProxy = UseProxy,
            ProxyType = ProxyType,
            ProxyHost = ProxyHost,
            ProxyPort = ProxyPort,
            ProxyTimeout = ProxyTimeout,
            UseCustomBackend = UseCustomBackend,
            BackendUrl = BackendUrl,
            EnableAI = EnableAI,
            AIProvider = AIProvider,
            EncryptedAIKey = _encryptedAIKey,
            AIModel = AIModel,
            AIApiBase = AIApiBase,
            AIProfiles = AIProfiles,
            CurrentAIProfile = CurrentAIProfile,
            Username = Username,
            EncryptedPassword = _encryptedPassword,
            BackendTimeout = BackendTimeout,
            // Keep the legacy fields populated for older clients that only support one account.
            WeChatAppId = currentWeChatAccount.AppId,
            WeChatApiBaseUrl = currentWeChatAccount.ApiBaseUrl,
            EncryptedWeChatApiAuthorization = currentWeChatAccount.EncryptedApiAuthorization,
            EncryptedWeChatAppSecret = currentWeChatAccount.EncryptedAppSecret,
            WeChatAuthor = currentWeChatAccount.Author,
            WeChatAccounts = WeChatAccounts,
            CurrentWeChatAccountId = CurrentWeChatAccountId,
            WeChatDefaultTheme = WeChatDefaultTheme,
            IsDarkTheme = IsDarkTheme,
            EnableRegexImageParsing = EnableRegexImageParsing,
            EditorFontSize = EditorFontSize,
            EditorWordWrap = EditorWordWrap,
            EditorShowLineNumbers = EditorShowLineNumbers
        };
    }

    // 将旧的AI设置迁移到配置文件
    private void MigrateToProfiles() {
        AIProfiles = new List<AIProfile>
        {
            new AIProfile
            {
                Name = "默认",
                EnableAI = this.EnableAI,
                Provider = this.AIProvider,
                Key = this.AIKey,
                Model = this.AIModel,
                ApiBase = this.AIApiBase
            }
        };
        CurrentAIProfile = "默认";
    }

    private void EnsureWeChatAccounts() {
        WeChatAccounts ??= new List<WeChatAccountProfile>();
        if (WeChatAccounts.Count == 0) {
            WeChatAccounts.Add(new WeChatAccountProfile {
                Name = "默认公众号",
                AppId = WeChatAppId,
                ApiBaseUrl = string.IsNullOrWhiteSpace(WeChatApiBaseUrl)
                    ? WeChatHttpClientRegistration.OfficialApiBaseUrl
                    : WeChatApiBaseUrl,
                Author = WeChatAuthor,
                EncryptedAppSecret = _encryptedWeChatAppSecret,
                EncryptedApiAuthorization = _encryptedWeChatApiAuthorization
            });
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in WeChatAccounts) {
            if (string.IsNullOrWhiteSpace(account.Id) || !ids.Add(account.Id)) {
                account.Id = Guid.NewGuid().ToString("N");
                ids.Add(account.Id);
            }
            if (string.IsNullOrWhiteSpace(account.ApiBaseUrl)) {
                account.ApiBaseUrl = WeChatHttpClientRegistration.OfficialApiBaseUrl;
            }
        }

        if (!WeChatAccounts.Any(profile => profile.Id == CurrentWeChatAccountId)) {
            CurrentWeChatAccountId = WeChatAccounts[0].Id;
        }
    }

    /// <summary>Persist an isolated edit before making it visible to running services.</summary>
    public bool TryUpdate(Action<AppSettings> update, out string? error) {
        error = null;
        if (HasLoadError) {
            error = "原配置文件加载失败，已停止写回。请先检查配置文件。";
            return false;
        }
        var candidate = FromSnapshot(ToSnapshot());
        candidate.AIProfiles = AIProfiles.Select(profile => profile.Clone()).ToList();
        candidate.WeChatAccounts = WeChatAccounts.Select(account => account.Clone()).ToList();
        update(candidate);
        var temporaryPath = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(candidate.ToSnapshot(), AppSettingsJsonContext.Default.AppSettingsSnapshot);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, ConfigPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            error = "无法写入配置文件，请检查文件权限和磁盘空间后重试。";
            return false;
        }
        finally {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        update(this);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Save() {
        if (HasLoadError) {
            Trace.TraceWarning(
                $"Skip saving app settings because the last load failed. File: {ConfigPath}");
            return;
        }

        try {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(ToSnapshot(), AppSettingsJsonContext.Default.AppSettingsSnapshot);
            File.WriteAllText(ConfigPath, json);

            // 触发配置变更事件
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception) {
            // todo 处理保存失败的情况
        }
    }

    private static string BuildLoadErrorMessage(Exception ex) {
        return $"加载配置失败，已回退到内存默认配置，并停止写回以避免覆盖原文件。配置文件: {ConfigPath}. 错误: {ex.Message}";
    }

    private static string ResolveConfigPath() {
        var overridePath = Environment.GetEnvironmentVariable(ConfigPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath)) {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StarBlogPublisher",
            "settings.json"
        );
    }
}

internal sealed class AppSettingsSnapshot {
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "http";
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public int ProxyTimeout { get; set; } = 30;
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    public string EncryptedAIKey { get; set; } = string.Empty;
    public string AIModel { get; set; } = string.Empty;
    public string AIApiBase { get; set; } = string.Empty;
    public List<AIProfile> AIProfiles { get; set; } = new();
    public string CurrentAIProfile { get; set; } = "默认";
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public int BackendTimeout { get; set; } = 30;
    public string WeChatAppId { get; set; } = string.Empty;
    public string WeChatApiBaseUrl { get; set; } = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    public string EncryptedWeChatApiAuthorization { get; set; } = string.Empty;
    public string EncryptedWeChatAppSecret { get; set; } = string.Empty;
    public string WeChatAuthor { get; set; } = string.Empty;
    public List<WeChatAccountProfile> WeChatAccounts { get; set; } = new();
    public string CurrentWeChatAccountId { get; set; } = string.Empty;
    public string WeChatDefaultTheme { get; set; } = "newspaper";
    public bool IsDarkTheme { get; set; }
    public bool EnableRegexImageParsing { get; set; }
    public double EditorFontSize { get; set; } = 14;
    public bool EditorWordWrap { get; set; } = true;
    public bool EditorShowLineNumbers { get; set; }
}

internal sealed class LegacyAppSettingsSnapshot {
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "http";
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public int ProxyTimeout { get; set; } = 30;
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    public string AIKey { get; set; } = string.Empty;
    public string EncryptedAIKey { get; set; } = string.Empty;
    public string AIModel { get; set; } = string.Empty;
    public string AIApiBase { get; set; } = string.Empty;
    public List<AIProfile> AIProfiles { get; set; } = new();
    public string CurrentAIProfile { get; set; } = "默认";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public int BackendTimeout { get; set; } = 30;
    public string WeChatAppId { get; set; } = string.Empty;
    public string WeChatApiBaseUrl { get; set; } = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    public string WeChatApiAuthorization { get; set; } = string.Empty;
    public string EncryptedWeChatApiAuthorization { get; set; } = string.Empty;
    public string WeChatAppSecret { get; set; } = string.Empty;
    public string EncryptedWeChatAppSecret { get; set; } = string.Empty;
    public string WeChatAuthor { get; set; } = string.Empty;
    public List<WeChatAccountProfile> WeChatAccounts { get; set; } = new();
    public string CurrentWeChatAccountId { get; set; } = string.Empty;
    public string WeChatDefaultTheme { get; set; } = "newspaper";
    public bool IsDarkTheme { get; set; }
    public bool EnableRegexImageParsing { get; set; }
    public double EditorFontSize { get; set; } = 14;
    public bool EditorWordWrap { get; set; } = true;
    public bool EditorShowLineNumbers { get; set; }

    public AppSettingsSnapshot ToAppSettingsSnapshot() {
        var weChatAccounts = WeChatAccounts ?? new List<WeChatAccountProfile>();
        if (weChatAccounts.Count == 0) {
            weChatAccounts.Add(new WeChatAccountProfile {
                Name = "默认公众号",
                AppId = WeChatAppId,
                ApiBaseUrl = string.IsNullOrWhiteSpace(WeChatApiBaseUrl)
                    ? WeChatHttpClientRegistration.OfficialApiBaseUrl
                    : WeChatApiBaseUrl,
                Author = WeChatAuthor,
                EncryptedAppSecret = !string.IsNullOrWhiteSpace(EncryptedWeChatAppSecret)
                    ? EncryptedWeChatAppSecret
                    : EncryptionService.Encrypt(WeChatAppSecret),
                EncryptedApiAuthorization = !string.IsNullOrWhiteSpace(EncryptedWeChatApiAuthorization)
                    ? EncryptedWeChatApiAuthorization
                    : EncryptionService.Encrypt(WeChatApiAuthorization)
            });
        }
        var currentWeChatAccountId = weChatAccounts.Any(account => account.Id == CurrentWeChatAccountId)
            ? CurrentWeChatAccountId
            : weChatAccounts[0].Id;

        return new AppSettingsSnapshot {
            UseProxy = UseProxy,
            ProxyType = ProxyType,
            ProxyHost = ProxyHost,
            ProxyPort = ProxyPort,
            ProxyTimeout = ProxyTimeout,
            UseCustomBackend = UseCustomBackend,
            BackendUrl = BackendUrl,
            EnableAI = EnableAI,
            AIProvider = AIProvider,
            EncryptedAIKey = !string.IsNullOrWhiteSpace(EncryptedAIKey)
                ? EncryptedAIKey
                : EncryptionService.Encrypt(AIKey),
            AIModel = AIModel,
            AIApiBase = AIApiBase,
            AIProfiles = AIProfiles,
            CurrentAIProfile = CurrentAIProfile,
            Username = Username,
            EncryptedPassword = !string.IsNullOrWhiteSpace(EncryptedPassword)
                ? EncryptedPassword
                : EncryptionService.Encrypt(Password),
            BackendTimeout = BackendTimeout,
            WeChatAppId = WeChatAppId,
            WeChatApiBaseUrl = WeChatApiBaseUrl,
            EncryptedWeChatApiAuthorization = !string.IsNullOrWhiteSpace(EncryptedWeChatApiAuthorization)
                ? EncryptedWeChatApiAuthorization
                : EncryptionService.Encrypt(WeChatApiAuthorization),
            EncryptedWeChatAppSecret = !string.IsNullOrWhiteSpace(EncryptedWeChatAppSecret)
                ? EncryptedWeChatAppSecret
                : EncryptionService.Encrypt(WeChatAppSecret),
            WeChatAuthor = WeChatAuthor,
            WeChatAccounts = weChatAccounts,
            CurrentWeChatAccountId = currentWeChatAccountId,
            WeChatDefaultTheme = WeChatDefaultTheme,
            IsDarkTheme = IsDarkTheme,
            EnableRegexImageParsing = EnableRegexImageParsing,
            EditorFontSize = EditorFontSize is >= 11 and <= 22 ? EditorFontSize : 14,
            EditorWordWrap = EditorWordWrap,
            EditorShowLineNumbers = EditorShowLineNumbers
        };
    }
}
