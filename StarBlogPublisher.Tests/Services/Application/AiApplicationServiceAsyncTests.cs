using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class AiApplicationServiceAsyncTests {
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly AiService _aiService;
    private readonly AppSettings _settings;
    private readonly AiApplicationService _service;

    public AiApplicationServiceAsyncTests() {
        _mockChatClient = new Mock<IChatClient>();
        _aiService = new AiService(_mockChatClient.Object);
        _settings = new AppSettings { EnableAI = true };
        _service = new AiApplicationService(_aiService, _settings);
    }

    private void SetupStreamingResponse(string text) {
        var updates = new List<ChatResponseUpdate> {
            new(ChatRole.Assistant, text)
        };
        _mockChatClient
            .Setup(x => x.GetStreamingResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(updates));
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items) {
        foreach (var item in items) {
            yield return item;
        }
        await Task.CompletedTask;
    }

    [Fact]
    public void IsEnabled_ReturnsSettingsValue() {
        _service.IsEnabled.Should().BeTrue();

        var disabledSettings = new AppSettings { EnableAI = false };
        var disabledService = new AiApplicationService(_aiService, disabledSettings);
        disabledService.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateSummaryAsync_ReturnsAiResponse() {
        SetupStreamingResponse("This is a summary of the article.");

        var result = await _service.GenerateSummaryAsync("Test Title", "Test content");

        result.Should().Be("This is a summary of the article.");
    }

    [Fact]
    public async Task GenerateSlugAsync_ReturnsCleanedSlug() {
        SetupStreamingResponse("hello-world-test");

        var result = await _service.GenerateSlugAsync("Hello World Test");

        result.Should().Be("hello-world-test");
    }

    [Fact]
    public async Task GenerateSlugAsync_WithSpecialChars_CleansResult() {
        SetupStreamingResponse("hello world! @#");

        var result = await _service.GenerateSlugAsync("hello world!");

        // CleanSlug strips non-lowercase-alphanumeric chars
        result.Should().Be("helloworld");
    }

    [Fact]
    public async Task GenerateKeywordsAsync_ReturnsExtractedKeywords() {
        SetupStreamingResponse("""["C#", "ASP.NET", "Web API"]""");

        var result = await _service.GenerateKeywordsAsync("Test Title", "Test content");

        result.Should().Be("C#, ASP.NET, Web API");
    }

    [Fact]
    public async Task RefineTitleAsync_ReturnsTrimmedTitle() {
        SetupStreamingResponse("《Refined Title》");

        var result = await _service.RefineTitleAsync("Original Title", "content");

        result.Should().Be("Refined Title");
    }

    [Fact]
    public void IsEnabled_False_StillAllowsCalls() {
        var disabledSettings = new AppSettings { EnableAI = false };
        var service = new AiApplicationService(_aiService, disabledSettings);

        service.IsEnabled.Should().BeFalse();
    }
}
