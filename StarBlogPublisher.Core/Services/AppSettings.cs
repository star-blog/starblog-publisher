using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

    // 主题设置
    public bool IsDarkTheme { get; set; } = false;

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
            IsDarkTheme = snapshot.IsDarkTheme,
            EnableRegexImageParsing = snapshot.EnableRegexImageParsing
        };
    }

    private AppSettingsSnapshot ToSnapshot() {
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
            IsDarkTheme = IsDarkTheme,
            EnableRegexImageParsing = EnableRegexImageParsing
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
    public bool IsDarkTheme { get; set; }
    public bool EnableRegexImageParsing { get; set; }
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
    public bool IsDarkTheme { get; set; }
    public bool EnableRegexImageParsing { get; set; }

    public AppSettingsSnapshot ToAppSettingsSnapshot() {
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
            IsDarkTheme = IsDarkTheme,
            EnableRegexImageParsing = EnableRegexImageParsing
        };
    }
}