using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Services;

public static class AppSettingsPortableTransfer {
    public const string FormatId = "starblog-publisher-settings";
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions ExportJsonOptions = new(PortableAppSettingsJsonContext.Default.Options) {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ExportJson(AppSettings settings) {
        var document = new PortableAppSettingsDocument {
            Format = FormatId,
            Version = CurrentVersion,
            ExportedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            Settings = CreatePayload(settings)
        };
        return JsonSerializer.Serialize(document, ExportJsonOptions);
    }

    public static bool TryParse(string json, out PortableAppSettingsDocument? document, out string? error) {
        document = null;
        error = null;
        if (string.IsNullOrWhiteSpace(json)) {
            error = "配置文件为空。";
            return false;
        }

        PortableAppSettingsDocument? parsed;
        try {
            parsed = JsonSerializer.Deserialize(json,
                PortableAppSettingsJsonContext.Default.PortableAppSettingsDocument);
        }
        catch (JsonException ex) {
            error = $"无法解析配置文件：{ex.Message}";
            return false;
        }

        if (parsed == null) {
            error = "配置文件为空，或无法解析为已知格式。";
            return false;
        }

        if (!string.Equals(parsed.Format, FormatId, StringComparison.Ordinal)) {
            error = "不是 StarBlog Publisher 可移植配置（format 不匹配）。请选择通过「导出配置」生成的 JSON 文件。";
            return false;
        }

        if (parsed.Version != CurrentVersion) {
            error = parsed.Version > CurrentVersion
                ? $"配置版本 {parsed.Version} 高于当前应用支持的版本 {CurrentVersion}，请升级 StarBlog Publisher 后再导入。"
                : $"不支持的配置版本：{parsed.Version}。";
            return false;
        }

        if (parsed.Settings == null) {
            error = "配置文件缺少 settings 节点。";
            return false;
        }

        document = parsed;
        return true;
    }

    public static bool TryApply(AppSettings target, PortableAppSettingsDocument document, out string? error) {
        error = null;
        if (AppSettings.HasLoadError) {
            error = "本机配置文件加载失败，已停止写回。请先修复 settings.json 后再导入。";
            return false;
        }

        if (!ValidatePayload(document.Settings!, out error)) {
            return false;
        }

        return target.TryUpdate(settings => ApplyPayload(settings, document.Settings!), out error);
    }

    private static PortableAppSettingsPayload CreatePayload(AppSettings settings) {
        var currentWeChat = settings.CurrentWeChatAccount;
        return new PortableAppSettingsPayload {
            UseProxy = settings.UseProxy,
            ProxyType = settings.ProxyType,
            ProxyHost = settings.ProxyHost,
            ProxyPort = settings.ProxyPort,
            ProxyTimeout = settings.ProxyTimeout,
            UseCustomBackend = settings.UseCustomBackend,
            BackendUrl = settings.BackendUrl,
            Username = settings.Username,
            Password = settings.Password,
            BackendTimeout = settings.BackendTimeout,
            EnableAI = settings.EnableAI,
            AIProvider = settings.AIProvider,
            AIKey = settings.AIKey,
            AIModel = settings.AIModel,
            AIApiBase = settings.AIApiBase,
            CurrentAIProfile = settings.CurrentAIProfile,
            AIProfiles = settings.AIProfiles.Select(CloneProfile).ToList(),
            WeChatDefaultTheme = settings.WeChatDefaultTheme,
            CurrentWeChatAccountId = settings.CurrentWeChatAccountId,
            WeChatAccounts = settings.WeChatAccounts.Select(CloneWeChatAccount).ToList(),
            IsDarkTheme = settings.IsDarkTheme,
            EnableRegexImageParsing = settings.EnableRegexImageParsing,
            EditorFontSize = settings.EditorFontSize,
            EditorWordWrap = settings.EditorWordWrap,
            EditorShowLineNumbers = settings.EditorShowLineNumbers,
            WeChatAppId = currentWeChat.AppId,
            WeChatApiBaseUrl = currentWeChat.ApiBaseUrl,
            WeChatApiAuthorization = currentWeChat.ApiAuthorization,
            WeChatAppSecret = currentWeChat.AppSecret,
            WeChatAuthor = currentWeChat.Author
        };
    }

    private static PortableAIProfile CloneProfile(AIProfile profile) => new() {
        Name = profile.Name,
        EnableAI = profile.EnableAI,
        Provider = profile.Provider,
        Key = profile.Key,
        Model = profile.Model,
        ApiBase = profile.ApiBase
    };

    private static PortableWeChatAccount CloneWeChatAccount(WeChatAccountProfile account) => new() {
        Id = account.Id,
        Name = account.Name,
        AppId = account.AppId,
        AppSecret = account.AppSecret,
        ApiBaseUrl = account.ApiBaseUrl,
        ApiAuthorization = account.ApiAuthorization,
        Author = account.Author
    };

    private static void ApplyPayload(AppSettings settings, PortableAppSettingsPayload payload) {
        settings.UseProxy = payload.UseProxy;
        settings.ProxyType = string.IsNullOrWhiteSpace(payload.ProxyType) ? "http" : payload.ProxyType;
        settings.ProxyHost = payload.ProxyHost ?? string.Empty;
        settings.ProxyPort = payload.ProxyPort;
        settings.ProxyTimeout = payload.ProxyTimeout;
        settings.UseCustomBackend = payload.UseCustomBackend;
        settings.BackendUrl = payload.UseCustomBackend ? (payload.BackendUrl ?? string.Empty).Trim() : string.Empty;
        settings.Username = payload.Username ?? string.Empty;
        settings.Password = payload.Password ?? string.Empty;
        settings.BackendTimeout = payload.BackendTimeout;
        settings.EnableRegexImageParsing = payload.EnableRegexImageParsing;
        settings.WeChatDefaultTheme = WeChatThemeCatalog.NormalizeId(payload.WeChatDefaultTheme);
        settings.IsDarkTheme = payload.IsDarkTheme;
        settings.EditorFontSize = payload.EditorFontSize is >= 11 and <= 22 ? payload.EditorFontSize : 14;
        settings.EditorWordWrap = payload.EditorWordWrap;
        settings.EditorShowLineNumbers = payload.EditorShowLineNumbers;

        settings.AIProfiles = payload.AIProfiles?.Select(MapProfile).ToList() ?? new List<AIProfile>();
        if (settings.AIProfiles.Count == 0) {
            settings.AIProfiles.Add(new AIProfile {
                Name = "默认",
                EnableAI = payload.EnableAI,
                Provider = payload.AIProvider ?? "openai",
                Key = payload.AIKey ?? string.Empty,
                Model = payload.AIModel ?? string.Empty,
                ApiBase = payload.AIApiBase ?? string.Empty
            });
        }

        settings.CurrentAIProfile = string.IsNullOrWhiteSpace(payload.CurrentAIProfile)
            ? settings.AIProfiles[0].Name
            : payload.CurrentAIProfile;
        var currentProfile = settings.AIProfiles.FirstOrDefault(profile => profile.Name == settings.CurrentAIProfile)
                             ?? settings.AIProfiles[0];
        settings.CurrentAIProfile = currentProfile.Name;
        settings.EnableAI = currentProfile.EnableAI;
        settings.AIProvider = currentProfile.Provider;
        settings.AIKey = currentProfile.Key;
        settings.AIModel = currentProfile.Model;
        settings.AIApiBase = currentProfile.ApiBase;

        settings.WeChatAccounts = payload.WeChatAccounts?.Select(MapWeChatAccount).ToList() ??
                                  new List<WeChatAccountProfile>();
        EnsureWeChatAccounts(settings, payload);
        settings.CurrentWeChatAccountId =
            settings.WeChatAccounts.Any(account => account.Id == payload.CurrentWeChatAccountId)
                ? payload.CurrentWeChatAccountId
                : settings.WeChatAccounts[0].Id;
    }

    private static AIProfile MapProfile(PortableAIProfile profile) => new() {
        Name = profile.Name ?? string.Empty,
        EnableAI = profile.EnableAI,
        Provider = string.IsNullOrWhiteSpace(profile.Provider) ? "openai" : profile.Provider,
        Key = profile.Key ?? string.Empty,
        Model = profile.Model ?? string.Empty,
        ApiBase = profile.ApiBase ?? string.Empty
    };

    private static WeChatAccountProfile MapWeChatAccount(PortableWeChatAccount account) => new() {
        Id = string.IsNullOrWhiteSpace(account.Id) ? Guid.NewGuid().ToString("N") : account.Id,
        Name = account.Name ?? string.Empty,
        AppId = account.AppId ?? string.Empty,
        ApiBaseUrl = string.IsNullOrWhiteSpace(account.ApiBaseUrl)
            ? WeChatHttpClientRegistration.OfficialApiBaseUrl
            : account.ApiBaseUrl,
        AppSecret = account.AppSecret ?? string.Empty,
        ApiAuthorization = account.ApiAuthorization ?? string.Empty,
        Author = account.Author ?? string.Empty
    };

    private static void EnsureWeChatAccounts(AppSettings settings, PortableAppSettingsPayload payload) {
        if (settings.WeChatAccounts.Count == 0) {
            settings.WeChatAccounts.Add(new WeChatAccountProfile {
                Name = "默认公众号",
                AppId = payload.WeChatAppId ?? string.Empty,
                ApiBaseUrl = string.IsNullOrWhiteSpace(payload.WeChatApiBaseUrl)
                    ? WeChatHttpClientRegistration.OfficialApiBaseUrl
                    : payload.WeChatApiBaseUrl,
                Author = payload.WeChatAuthor ?? string.Empty,
                AppSecret = payload.WeChatAppSecret ?? string.Empty,
                ApiAuthorization = payload.WeChatApiAuthorization ?? string.Empty
            });
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in settings.WeChatAccounts) {
            if (string.IsNullOrWhiteSpace(account.Id) || !ids.Add(account.Id)) {
                account.Id = Guid.NewGuid().ToString("N");
                ids.Add(account.Id);
            }

            if (string.IsNullOrWhiteSpace(account.ApiBaseUrl)) {
                account.ApiBaseUrl = WeChatHttpClientRegistration.OfficialApiBaseUrl;
            }
        }
    }

    private static bool ValidatePayload(PortableAppSettingsPayload payload, out string? error) {
        error = null;
        if (payload.UseCustomBackend && !IsHttpUrl(payload.BackendUrl)) {
            error = "博客连接：服务 URL 必须是完整的 HTTP 或 HTTPS 地址。";
            return false;
        }

        if (payload.BackendTimeout is < 1 or > 600) {
            error = "博客连接：请求超时须在 1 至 600 秒之间。";
            return false;
        }

        if (payload.UseProxy && (string.IsNullOrWhiteSpace(payload.ProxyHost)
                                 || payload.ProxyPort is < 1 or > 65535
                                 || payload.ProxyTimeout is < 1 or > 600)) {
            error = "网络代理：请填写有效的主机名、端口（1–65535）和超时（1–600 秒）。";
            return false;
        }

        var accounts = payload.WeChatAccounts ?? new List<PortableWeChatAccount>();
        foreach (var account in accounts) {
            if (string.IsNullOrWhiteSpace(account.Name)) {
                error = "微信公众号：账号名称不能为空。";
                return false;
            }

            if (accounts.Count(a => string.Equals(a.Name?.Trim(), account.Name.Trim(), StringComparison.Ordinal)) > 1) {
                error = $"微信公众号：账号名称“{account.Name}”重复。";
                return false;
            }

            if (!WeChatHttpClientRegistration.TryGetApiBaseAddress(account.ApiBaseUrl, out _)) {
                error = $"微信公众号：账号“{account.Name}”的中转地址无效。";
                return false;
            }

            if (!WeChatHttpClientRegistration.IsValidApiAuthorization(account.ApiAuthorization)) {
                error = $"微信公众号：账号“{account.Name}”的认证值无效。";
                return false;
            }
        }

        foreach (var profile in payload.AIProfiles ?? new List<PortableAIProfile>()) {
            if (profile.EnableAI && string.Equals(profile.Provider, "custom", StringComparison.Ordinal)
                                 && !IsHttpUrl(profile.ApiBase)) {
                error = $"AI 创作：方案“{profile.Name}”的 API Base URL 无效。";
                return false;
            }
        }

        return true;
    }

    private static bool IsHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                                                    && (uri.Scheme == "http" || uri.Scheme == "https")
                                                    && !string.IsNullOrWhiteSpace(uri.Host);
}

public sealed class PortableAppSettingsDocument {
    public string Format { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ExportedAt { get; set; } = string.Empty;
    public PortableAppSettingsPayload? Settings { get; set; }
}

public sealed class PortableAppSettingsPayload {
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "http";
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public int ProxyTimeout { get; set; } = 30;
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int BackendTimeout { get; set; } = 30;
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    public string AIKey { get; set; } = string.Empty;
    public string AIModel { get; set; } = string.Empty;
    public string AIApiBase { get; set; } = string.Empty;
    public string CurrentAIProfile { get; set; } = "默认";
    public List<PortableAIProfile> AIProfiles { get; set; } = new();
    public string WeChatDefaultTheme { get; set; } = "newspaper";
    public string CurrentWeChatAccountId { get; set; } = string.Empty;
    public List<PortableWeChatAccount> WeChatAccounts { get; set; } = new();
    public bool IsDarkTheme { get; set; }
    public bool EnableRegexImageParsing { get; set; }
    public double EditorFontSize { get; set; } = 14;
    public bool EditorWordWrap { get; set; } = true;
    public bool EditorShowLineNumbers { get; set; }
    public string WeChatAppId { get; set; } = string.Empty;
    public string WeChatApiBaseUrl { get; set; } = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    public string WeChatApiAuthorization { get; set; } = string.Empty;
    public string WeChatAppSecret { get; set; } = string.Empty;
    public string WeChatAuthor { get; set; } = string.Empty;
}

public sealed class PortableAIProfile {
    public string Name { get; set; } = string.Empty;
    public bool EnableAI { get; set; } = true;
    public string Provider { get; set; } = "openai";
    public string Key { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiBase { get; set; } = string.Empty;
}

public sealed class PortableWeChatAccount {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = WeChatHttpClientRegistration.OfficialApiBaseUrl;
    public string ApiAuthorization { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PortableAppSettingsDocument))]
[JsonSerializable(typeof(PortableAppSettingsPayload))]
[JsonSerializable(typeof(PortableAIProfile))]
[JsonSerializable(typeof(List<PortableAIProfile>))]
[JsonSerializable(typeof(PortableWeChatAccount))]
[JsonSerializable(typeof(List<PortableWeChatAccount>))]
internal partial class PortableAppSettingsJsonContext : JsonSerializerContext;
