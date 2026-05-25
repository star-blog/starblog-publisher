using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Services;

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
        snapshot.IsDarkTheme.Should().BeTrue();
        snapshot.EnableRegexImageParsing.Should().BeTrue();
    }
}