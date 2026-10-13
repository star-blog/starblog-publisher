using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Services;

[Collection("AppSettings")]
public class AppSettingsCompatibilityTests {
    [Fact]
    public void DeserializeSnapshot_ShouldReadLegacyPasswordAndAiKeyFields() {
        var legacyJson = """
        {
          "UseProxy": true,
          "ProxyType": "socks5",
          "ProxyHost": "127.0.0.1",
          "ProxyPort": 7890,
          "UseCustomBackend": true,
          "BackendUrl": "https://blog.example.com",
          "EnableAI": true,
          "AIProvider": "openai",
          "AIKey": "plain-ai-key",
          "AIModel": "gpt-4.1",
          "AIApiBase": "https://api.example.com",
          "AIProfiles": [
            {
              "Name": "默认",
              "EnableAI": true,
              "Provider": "openai",
              "Key": "profile-key",
              "Model": "gpt-4.1-mini",
              "ApiBase": "https://api.example.com/v1"
            }
          ],
          "CurrentAIProfile": "默认",
          "Username": "alice",
          "Password": "plain-password",
          "BackendTimeout": 45,
          "WeChatApiBaseUrl": "https://wechat-proxy.example.com/api/",
          "WeChatApiAuthorization": "Bearer relay-token",
          "IsDarkTheme": true,
          "EnableRegexImageParsing": true
        }
        """;

        var snapshot = AppSettings.DeserializeSnapshot(legacyJson);

        snapshot.UseProxy.Should().BeTrue();
        snapshot.ProxyType.Should().Be("socks5");
        snapshot.ProxyHost.Should().Be("127.0.0.1");
        snapshot.ProxyPort.Should().Be(7890);
        snapshot.UseCustomBackend.Should().BeTrue();
        snapshot.BackendUrl.Should().Be("https://blog.example.com");
        snapshot.EnableAI.Should().BeTrue();
        snapshot.AIProvider.Should().Be("openai");
        snapshot.EncryptedAIKey.Should().NotBeNullOrWhiteSpace();
        snapshot.EncryptedAIKey.Should().NotBe("plain-ai-key");
        snapshot.AIModel.Should().Be("gpt-4.1");
        snapshot.AIApiBase.Should().Be("https://api.example.com");
        snapshot.AIProfiles.Should().ContainSingle();
        snapshot.AIProfiles[0].Should().BeEquivalentTo(new AIProfile {
            Name = "默认",
            EnableAI = true,
            Provider = "openai",
            Key = "profile-key",
            Model = "gpt-4.1-mini",
            ApiBase = "https://api.example.com/v1"
        });
        snapshot.CurrentAIProfile.Should().Be("默认");
        snapshot.Username.Should().Be("alice");
        snapshot.EncryptedPassword.Should().NotBeNullOrWhiteSpace();
        snapshot.EncryptedPassword.Should().NotBe("plain-password");
        snapshot.BackendTimeout.Should().Be(45);
        snapshot.WeChatApiBaseUrl.Should().Be("https://wechat-proxy.example.com/api/");
        snapshot.EncryptedWeChatApiAuthorization.Should().NotBeNullOrWhiteSpace();
        snapshot.EncryptedWeChatApiAuthorization.Should().NotBe("Bearer relay-token");
        snapshot.WeChatAccounts.Should().ContainSingle();
        snapshot.CurrentWeChatAccountId.Should().Be(snapshot.WeChatAccounts[0].Id);
        snapshot.WeChatAccounts[0].ApiBaseUrl.Should().Be("https://wechat-proxy.example.com/api/");
        snapshot.WeChatAccounts[0].ApiAuthorization.Should().Be("Bearer relay-token");
        snapshot.ThemeMode.Should().Be(ThemeMode.Dark);
        snapshot.IsDarkTheme.Should().BeTrue();
        snapshot.EnableRegexImageParsing.Should().BeTrue();
    }

    [Fact]
    public void DeserializeSnapshot_LegacyIsDarkThemeFalse_MapsToLight() {
        var snapshot = AppSettings.DeserializeSnapshot("""{ "IsDarkTheme": false }""");

        snapshot.ThemeMode.Should().Be(ThemeMode.Light);
        snapshot.IsDarkTheme.Should().BeFalse();
    }

    [Fact]
    public void DeserializeSnapshot_ThemeMode_TakesPrecedenceOverLegacyFlag() {
        var snapshot = AppSettings.DeserializeSnapshot("""
            {
              "ThemeMode": "System",
              "IsDarkTheme": true
            }
            """);

        snapshot.ThemeMode.Should().Be(ThemeMode.System);
        snapshot.IsDarkTheme.Should().BeFalse();
    }

    [Fact]
    public void DeserializeSnapshot_ExplicitLight_KeepsForcedLight() {
        var snapshot = AppSettings.DeserializeSnapshot("""
            {
              "ThemeMode": "Light",
              "IsDarkTheme": true
            }
            """);

        snapshot.ThemeMode.Should().Be(ThemeMode.Light);
        snapshot.IsDarkTheme.Should().BeFalse();
    }

    [Fact]
    public void DeserializeSnapshot_MissingInterfaceStyle_DefaultsToStandard() {
        var snapshot = AppSettings.DeserializeSnapshot("""
            {
              "ThemeMode": "Dark",
              "IsDarkTheme": true
            }
            """);

        snapshot.ThemeMode.Should().Be(ThemeMode.Dark);
        snapshot.IsDarkTheme.Should().BeTrue();
        snapshot.InterfaceStyle.Should().Be(InterfaceStyle.Standard);
    }

    [Fact]
    public void DeserializeSnapshot_UnknownInterfaceStyleString_FallsBackToStandard() {
        var snapshot = AppSettings.DeserializeSnapshot("""
            {
              "ThemeMode": "Dark",
              "InterfaceStyle": "Glass"
            }
            """);

        snapshot.ThemeMode.Should().Be(ThemeMode.Dark);
        snapshot.InterfaceStyle.Should().Be(InterfaceStyle.Standard);
    }

    [Fact]
    public void DeserializeSnapshot_UnknownInterfaceStyleNumber_FallsBackToStandard() {
        var snapshot = AppSettings.DeserializeSnapshot("""
            {
              "ThemeMode": "Light",
              "InterfaceStyle": 9
            }
            """);

        snapshot.ThemeMode.Should().Be(ThemeMode.Light);
        snapshot.InterfaceStyle.Should().Be(InterfaceStyle.Standard);
    }

    [Fact]
    public void DeserializeSnapshot_UnknownThemeModeString_Throws() {
        var act = () => AppSettings.DeserializeSnapshot("""{ "ThemeMode": "Solarized" }""");
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData(ThemeMode.System, InterfaceStyle.Standard)]
    [InlineData(ThemeMode.System, InterfaceStyle.Mica)]
    [InlineData(ThemeMode.System, InterfaceStyle.Acrylic)]
    [InlineData(ThemeMode.Light, InterfaceStyle.Standard)]
    [InlineData(ThemeMode.Light, InterfaceStyle.Mica)]
    [InlineData(ThemeMode.Light, InterfaceStyle.Acrylic)]
    [InlineData(ThemeMode.Dark, InterfaceStyle.Standard)]
    [InlineData(ThemeMode.Dark, InterfaceStyle.Mica)]
    [InlineData(ThemeMode.Dark, InterfaceStyle.Acrylic)]
    public void TryUpdate_NineAppearanceCombinations_RoundTrip(ThemeMode themeMode, InterfaceStyle interfaceStyle) {
        WithIsolatedSettings(settings => {
            settings.TryUpdate(target => {
                target.ThemeMode = themeMode;
                target.InterfaceStyle = interfaceStyle;
            }, out var error).Should().BeTrue(error);

            ResetAppSettingsSingleton();
            var reloaded = AppSettings.Instance;
            reloaded.ThemeMode.Should().Be(themeMode);
            reloaded.IsDarkTheme.Should().Be(ThemeModeHelper.ToLegacyIsDarkTheme(themeMode));
            reloaded.InterfaceStyle.Should().Be(interfaceStyle);
        });
    }

    private static void WithIsolatedSettings(Action<AppSettings> action) {
        var path = Path.Combine(Path.GetTempPath(), "starblog-settings-compat-tests", Guid.NewGuid() + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var originalPath = Environment.GetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH");
        ResetAppSettingsSingleton();
        try {
            Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", path);
            ResetAppSettingsSingleton();
            action(AppSettings.Instance);
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
