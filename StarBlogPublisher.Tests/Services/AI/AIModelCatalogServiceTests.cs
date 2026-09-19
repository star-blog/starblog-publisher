using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.AI;

namespace StarBlogPublisher.Tests.Services.AI;

public class AIModelCatalogServiceTests {
    [Fact]
    public async Task GetModelsAsync_WithoutApiKey_ReturnsUnverifiedRecommendations() {
        var provider = new AIProviderInfo {
            Name = "openrouter",
            DefaultApiBase = "https://openrouter.ai/api/v1/",
            DefaultModels = ["openai/gpt-4.1-mini"]
        };

        var result = await new AIModelCatalogService().GetModelsAsync(provider, "", provider.DefaultApiBase);

        result.Status.Should().Be(AIModelCatalogStatus.MissingApiKey);
        result.Models.Should().ContainSingle().Which.Source.Should().Be(AIModelSource.Recommended);
    }

    [Fact]
    public async Task GetModelsAsync_OpenRouterCatalog_ParsesPricesAndContextWindow() {
        var handler = new StubHandler("""
            { "data": [{ "id": "openai/gpt-4.1-mini", "name": "GPT-4.1 mini", "context_length": 1048576,
              "pricing": { "prompt": "0.0000004", "completion": "0.0000016" } }] }
            """);
        var provider = new AIProviderInfo {
            Name = "openrouter",
            DefaultApiBase = "https://openrouter.ai/api/v1"
        };

        var result = await new AIModelCatalogService(handler).GetModelsAsync(provider, "test-key", provider.DefaultApiBase);

        result.Status.Should().Be(AIModelCatalogStatus.Available);
        var model = result.Models.Should().ContainSingle().Which;
        model.Id.Should().Be("openai/gpt-4.1-mini");
        model.ContextLength.Should().Be(1_048_576);
        model.InputPricePerMillionUsd.Should().Be(0.4m);
        model.OutputPricePerMillionUsd.Should().Be(1.6m);
        handler.RequestUri!.AbsolutePath.Should().Be("/api/v1/models");
        handler.Authorization!.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task GetModelsAsync_FailedCatalog_DoesNotReportRecommendationsAsAvailable() {
        var provider = new AIProviderInfo {
            Name = "openrouter",
            DefaultApiBase = "https://openrouter.ai/api/v1/",
            DefaultModels = ["openai/gpt-4.1-mini"]
        };

        var result = await new AIModelCatalogService(new StubHandler("{}", HttpStatusCode.Unauthorized))
            .GetModelsAsync(provider, "bad-key", provider.DefaultApiBase);

        result.Status.Should().Be(AIModelCatalogStatus.Failed);
        result.IsAvailable.Should().BeFalse();
        result.Models.Should().ContainSingle().Which.Source.Should().Be(AIModelSource.Recommended);
    }

    private sealed class StubHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler {
        public Uri? RequestUri { get; private set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(statusCode) {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }
}
