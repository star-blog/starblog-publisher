using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Services;

public class WeChatHttpClientRegistrationTests {
    [Fact]
    public void GetApiBaseAddress_UsesConfiguredProxyAndPreservesItsPath() {
        var settings = new AppSettings { WeChatApiBaseUrl = "https://wechat-proxy.example.com/wechat" };

        WeChatHttpClientRegistration.GetApiBaseAddress(settings)
            .Should().Be(new Uri("https://wechat-proxy.example.com/wechat/"));
    }

    [Fact]
    public void GetApiBaseAddress_FallsBackToOfficialUrlWhenValueIsInvalid() {
        var settings = new AppSettings { WeChatApiBaseUrl = "not a URL" };

        WeChatHttpClientRegistration.GetApiBaseAddress(settings)
            .Should().Be(new Uri(WeChatHttpClientRegistration.OfficialApiBaseUrl));
    }

    [Fact]
    public void TryGetApiBaseAddress_RejectsInvalidConfiguredUrl() {
        WeChatHttpClientRegistration.TryGetApiBaseAddress("not a URL", out _).Should().BeFalse();
    }

    [Fact]
    public void RegisteredApiClient_UsesTheLatestConfiguredBaseUrl() {
        var settings = new AppSettings { WeChatApiBaseUrl = "https://first-proxy.example.com/" };
        var services = new ServiceCollection();
        services.AddWeChatHttpClients(settings);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        factory.CreateClient(WeChatHttpClientRegistration.ApiClientName).BaseAddress
            .Should().Be(new Uri("https://first-proxy.example.com/"));

        settings.WeChatApiBaseUrl = "https://second-proxy.example.com/wechat";

        factory.CreateClient(WeChatHttpClientRegistration.ApiClientName).BaseAddress
            .Should().Be(new Uri("https://second-proxy.example.com/wechat/"));
    }

    [Fact]
    public void RegisteredApiClient_AppliesConfiguredRelayAuthorizationHeader() {
        var settings = new AppSettings { WeChatApiAuthorization = "Bearer relay-token" };
        var services = new ServiceCollection();
        services.AddWeChatHttpClients(settings);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(WeChatHttpClientRegistration.ApiClientName);

        client.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        client.DefaultRequestHeaders.Authorization.Parameter.Should().Be("relay-token");
    }

    [Theory]
    [InlineData("Bearer relay-token")]
    [InlineData("Basic cmVsYXk6dG9rZW4=")]
    public void IsValidApiAuthorization_AcceptsValidAuthorizationValues(string value) {
        WeChatHttpClientRegistration.IsValidApiAuthorization(value).Should().BeTrue();
    }

    [Fact]
    public void IsValidApiAuthorization_RejectsMalformedAuthorizationValue() {
        WeChatHttpClientRegistration.IsValidApiAuthorization("Bearer\r\nmalicious").Should().BeFalse();
    }
}
