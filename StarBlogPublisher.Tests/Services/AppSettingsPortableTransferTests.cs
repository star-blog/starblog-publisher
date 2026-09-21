using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Services;

public sealed class AppSettingsPortableTransferTests {
    [Fact]
    public void ExportAndApply_RoundTrip_PreservesPlaintextFields() {
        WithIsolatedSettings(settings => {
            settings.TryUpdate(target => {
                target.UseProxy = true;
                target.ProxyType = "socks5";
                target.ProxyHost = "127.0.0.1";
                target.ProxyPort = 7890;
                target.ProxyTimeout = 45;
                target.UseCustomBackend = true;
                target.BackendUrl = "https://blog.example.com";
                target.Username = "alice";
                target.Password = "secret-password";
                target.BackendTimeout = 50;
                target.EnableRegexImageParsing = true;
                target.WeChatDefaultTheme = "warm-card";
                target.IsDarkTheme = true;
                target.EditorFontSize = 16;
                target.EditorWordWrap = false;
                target.EditorShowLineNumbers = true;
                target.AIProfiles = [
                    new AIProfile {
                        Name = "默认",
                        EnableAI = true,
                        Provider = "openai",
                        Key = "ai-key-1",
                        Model = "gpt-4.1",
                        ApiBase = "https://api.example.com/v1"
                    },
                    new AIProfile {
                        Name = "备用",
                        EnableAI = false,
                        Provider = "custom",
                        Key = "ai-key-2",
                        Model = "local",
                        ApiBase = "https://llm.example.com/v1"
                    }
                ];
                target.CurrentAIProfile = "备用";
                target.EnableAI = false;
                target.AIProvider = "custom";
                target.AIKey = "ai-key-2";
                target.AIModel = "local";
                target.AIApiBase = "https://llm.example.com/v1";
                target.WeChatAccounts = [
                    new WeChatAccountProfile {
                        Name = "主号",
                        AppId = "wx-main",
                        AppSecret = "main-secret",
                        ApiBaseUrl = "https://api.weixin.qq.com/",
                        Author = "Alice"
                    },
                    new WeChatAccountProfile {
                        Name = "副号",
                        AppId = "wx-sub",
                        AppSecret = "sub-secret",
                        ApiBaseUrl = "https://relay.example.com/",
                        ApiAuthorization = "Bearer relay-token",
                        Author = "Bob"
                    }
                ];
                target.CurrentWeChatAccountId = target.WeChatAccounts[1].Id;
            }, out _).Should().BeTrue();

            var exported = AppSettingsPortableTransfer.ExportJson(settings);
            exported.Should().Contain(AppSettingsPortableTransfer.FormatId);

            settings.TryUpdate(target => {
                target.Username = "mutated";
                target.Password = "mutated";
            }, out _).Should().BeTrue();

            AppSettingsPortableTransfer.TryParse(exported, out var document, out var parseError).Should().BeTrue();
            parseError.Should().BeNull();
            AppSettingsPortableTransfer.TryApply(settings, document!, out var applyError).Should().BeTrue();
            applyError.Should().BeNull();

            settings.Username.Should().Be("alice");
            settings.Password.Should().Be("secret-password");
            settings.ProxyPort.Should().Be(7890);
            settings.BackendUrl.Should().Be("https://blog.example.com");
            settings.CurrentAIProfile.Should().Be("备用");
            settings.AIProfiles.Should().HaveCount(2);
            settings.AIProfiles[1].Key.Should().Be("ai-key-2");
            settings.WeChatAccounts.Should().HaveCount(2);
            settings.CurrentWeChatAccount.AppId.Should().Be("wx-sub");
            settings.CurrentWeChatAccount.AppSecret.Should().Be("sub-secret");
            settings.WeChatDefaultTheme.Should().Be("warm-card");
            settings.EditorFontSize.Should().Be(16);
            settings.EditorShowLineNumbers.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData("""{"format":"wrong","version":1,"settings":{}}""", "format")]
    [InlineData("""{"format":"starblog-publisher-settings","version":2,"settings":{}}""", "版本")]
    [InlineData("""{"format":"starblog-publisher-settings","version":1}""", "settings")]
    public void TryParse_RejectsInvalidDocuments(string json, string expectedFragment) {
        AppSettingsPortableTransfer.TryParse(json, out var document, out var error).Should().BeFalse();
        document.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
        error!.Should().Contain(expectedFragment, because: error);
    }

    [Fact]
    public void TryApply_DoesNotWriteDisk_WhenValidationFails() {
        WithIsolatedSettings((settings, path) => {
            settings.TryUpdate(target => target.Username = "baseline", out _).Should().BeTrue();
            var before = File.ReadAllText(path);

            var invalid = AppSettingsPortableTransfer.ExportJson(settings);
            var root = JsonSerializer.Deserialize<JsonElement>(invalid);
            var payload = root.GetProperty("settings");
            var mutated = new {
                format = AppSettingsPortableTransfer.FormatId,
                version = AppSettingsPortableTransfer.CurrentVersion,
                exportedAt = root.GetProperty("exportedAt").GetString(),
                settings = new {
                    useProxy = payload.GetProperty("useProxy").GetBoolean(),
                    proxyType = payload.GetProperty("proxyType").GetString(),
                    proxyHost = payload.GetProperty("proxyHost").GetString(),
                    proxyPort = payload.GetProperty("proxyPort").GetInt32(),
                    proxyTimeout = payload.GetProperty("proxyTimeout").GetInt32(),
                    useCustomBackend = true,
                    backendUrl = "not-a-url",
                    username = payload.GetProperty("username").GetString(),
                    password = payload.GetProperty("password").GetString(),
                    backendTimeout = payload.GetProperty("backendTimeout").GetInt32(),
                    enableAI = payload.GetProperty("enableAI").GetBoolean(),
                    aiProvider = payload.GetProperty("aiProvider").GetString(),
                    aiKey = payload.GetProperty("aiKey").GetString(),
                    aiModel = payload.GetProperty("aiModel").GetString(),
                    aiApiBase = payload.GetProperty("aiApiBase").GetString(),
                    currentAIProfile = payload.GetProperty("currentAIProfile").GetString(),
                    aiProfiles = JsonSerializer.Deserialize<object[]>(payload.GetProperty("aiProfiles").GetRawText()),
                    weChatDefaultTheme = payload.GetProperty("weChatDefaultTheme").GetString(),
                    currentWeChatAccountId = payload.GetProperty("currentWeChatAccountId").GetString(),
                    weChatAccounts = JsonSerializer.Deserialize<object[]>(payload.GetProperty("weChatAccounts").GetRawText()),
                    isDarkTheme = payload.GetProperty("isDarkTheme").GetBoolean(),
                    enableRegexImageParsing = payload.GetProperty("enableRegexImageParsing").GetBoolean(),
                    editorFontSize = payload.GetProperty("editorFontSize").GetDouble(),
                    editorWordWrap = payload.GetProperty("editorWordWrap").GetBoolean(),
                    editorShowLineNumbers = payload.GetProperty("editorShowLineNumbers").GetBoolean()
                }
            };
            var invalidJson = JsonSerializer.Serialize(mutated, new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            AppSettingsPortableTransfer.TryParse(invalidJson, out var document, out _).Should().BeTrue();
            AppSettingsPortableTransfer.TryApply(settings, document!, out var applyError).Should().BeFalse();
            applyError.Should().NotBeNullOrWhiteSpace();
            File.ReadAllText(path).Should().Be(before);
            settings.Username.Should().Be("baseline");
        });
    }

    private static void WithIsolatedSettings(Action<AppSettings> action) =>
        WithIsolatedSettings((settings, _) => action(settings));

    private static void WithIsolatedSettings(Action<AppSettings, string> action) {
        var path = Path.Combine(Path.GetTempPath(), "starblog-portable-tests", Guid.NewGuid() + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var originalPath = Environment.GetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH");
        ResetAppSettingsSingleton();
        try {
            Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", path);
            ResetAppSettingsSingleton();
            action(AppSettings.Instance, path);
        }
        finally {
            Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", originalPath);
            ResetAppSettingsSingleton();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void ResetAppSettingsSingleton() {
        var type = typeof(AppSettings);
        type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        type.GetField("_loadErrorMessage", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
    }
}
