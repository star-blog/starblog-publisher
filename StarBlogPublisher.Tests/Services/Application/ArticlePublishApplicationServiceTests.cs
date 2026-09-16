using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;

namespace StarBlogPublisher.Tests.Services.Application;

public class ArticlePublishApplicationServiceTests {
    private readonly AppSettings _settings;
    private readonly GlobalState _globalState;
    private readonly Mock<IAuth> _mockAuth;
    private readonly Mock<IBlogPost> _mockBlogPost;
    private readonly ApiService _apiService;
    private readonly AuthApplicationService _authService;
    private readonly ArticlePublishApplicationService _publishService;

    public ArticlePublishApplicationServiceTests() {
        _settings = new AppSettings();
        _globalState = new GlobalState();
        _mockAuth = new Mock<IAuth>();
        _mockBlogPost = new Mock<IBlogPost>();
        _apiService = new ApiService(
            _mockAuth.Object,
            _mockBlogPost.Object,
            Mock.Of<ICategory>()
        );
        _authService = new AuthApplicationService(_settings, _globalState, _apiService);
        _publishService = new ArticlePublishApplicationService(_apiService, _authService, _settings);
    }

    private void SetupLoggedIn() {
        _settings.Username = "user";
        _settings.Password = "pass";
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "test-token" }
            });
        _authService.LoginAsync("user", "pass").Wait();
    }

    private const string OriginalMarkdown = "# Test\nNo images here.";
    private const string ServerRewrittenContent = "# Test\n![cover](https://blog.example.com/media/cover.png)";

    private async Task<PublishResult> PublishMarkdownAsync(string markdown, bool publish = true) {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, markdown);
        try {
            return await _publishService.PublishAsync(
                tempFile, "Title", markdown, "summary", 1, publish: publish);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    private void SetupCreatedPost(string content, bool isPublish = true, string id = "post-1") {
        _mockBlogPost.Setup(x => x.Add(It.IsAny<PostCreationDto>()))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost {
                    Id = id,
                    Title = "Title",
                    Content = content,
                    IsPublish = isPublish,
                    CategoryId = 1
                }
            });
    }

    private sealed class CapturedContent {
        public string? Value { get; set; }
    }

    private CapturedContent SetupUpdateAndCapture(string id = "post-1") {
        var captured = new CapturedContent();
        _mockBlogPost.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<PostUpdateDto>()))
            .Callback<string, PostUpdateDto>((_, dto) => captured.Value = dto.Content)
            .ReturnsAsync((string _, PostUpdateDto dto) => new ApiResponse<BlogPost> {
                Data = new BlogPost {
                    Id = id,
                    Title = dto.Title,
                    Content = "from-update-response",
                    IsPublish = dto.IsPublish,
                    CategoryId = dto.CategoryId,
                    Slug = dto.Slug,
                    Status = dto.Status
                }
            });
        return captured;
    }

    [Fact]
    public void PublishResult_Ok_PreservesArticleUrlAndMarkdownContent() {
        var post = new BlogPost { Id = "post-1", Title = "Title" };

        var result = PublishResult.Ok(post, "https://example.com/p/article", "# Published article");

        result.Success.Should().BeTrue();
        result.PostUrl.Should().Be("https://example.com/p/article");
        result.MarkdownContent.Should().Be("# Published article");
    }

    // === PublishAsync validation ===

    [Fact]
    public async Task PublishAsync_EmptyContent_ReturnsFail() {
        SetupLoggedIn();

        var result = await _publishService.PublishAsync(
            "test.md", "Title", "", "summary", 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("文章内容为空");
    }

    [Fact]
    public async Task PublishAsync_InvalidCategoryId_ReturnsFail() {
        SetupLoggedIn();

        var result = await _publishService.PublishAsync(
            "test.md", "Title", "content", "summary", 0);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("请选择文章分类");
    }

    [Fact]
    public async Task PublishAsync_NegativeCategoryId_ReturnsFail() {
        SetupLoggedIn();

        var result = await _publishService.PublishAsync(
            "test.md", "Title", "content", "summary", -1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("请选择文章分类");
    }

    [Fact]
    public async Task PublishAsync_NotLoggedIn_ReturnsFail() {
        // Don't setup login
        var result = await _publishService.PublishAsync(
            "test.md", "Title", "content", "summary", 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PublishAsync_CreatePostFails_ReturnsFail() {
        SetupLoggedIn();

        _mockBlogPost.Setup(x => x.Add(It.IsAny<PostCreationDto>()))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = null,
                Message = "Server error"
            });

        // Need a temp file for MarkdownProcessor
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "# Test\nNo images here.");

        try {
            var result = await _publishService.PublishAsync(
                tempFile, "Title", "# Test\nNo images here.", "summary", 1);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("创建文章失败");
        }
        finally {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_NoImages_SendsUpdateWhenDraftMode() {
        SetupLoggedIn();

        var blogPost = new BlogPost {
            Id = "post-1",
            Title = "Title",
            Content = "# Test\nNo images.",
            IsPublish = true, // Server default is published
            CategoryId = 1
        };

        _mockBlogPost.Setup(x => x.Add(It.IsAny<PostCreationDto>()))
            .ReturnsAsync(new ApiResponse<BlogPost> { Data = blogPost });

        _mockBlogPost.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<PostUpdateDto>()))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost { Id = "post-1", Content = "# Test\nNo images.", IsPublish = false }
            });

        _mockBlogPost.Setup(x => x.Get(It.IsAny<string>()))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost { Id = "post-1", Content = "# Test\nNo images.", IsPublish = false }
            });

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "# Test\nNo images here.");

        try {
            // publish=false (draft mode), but server created as published
            var result = await _publishService.PublishAsync(
                tempFile, "Title", "# Test\nNo images here.", "summary", 1,
                publish: false);

            // Should have attempted the update to fix the publish status
            _mockBlogPost.Verify(
                x => x.Update("post-1", It.Is<PostUpdateDto>(d => d.IsPublish == false)),
                Times.Once);
            _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
        }
        finally {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PublishAsync_GetReturnsServerContent_UsesServerContent() {
        SetupLoggedIn();
        SetupCreatedPost(OriginalMarkdown);
        SetupUpdateAndCapture();
        _mockBlogPost.Setup(x => x.Get("post-1"))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost {
                    Id = "post-1",
                    Title = "Title",
                    Content = ServerRewrittenContent,
                    IsPublish = true
                }
            });

        var result = await PublishMarkdownAsync(OriginalMarkdown);

        result.Success.Should().BeTrue();
        result.Post!.Content.Should().Be(ServerRewrittenContent);
        _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_GetThrows_FallsBackToProcessedContent() {
        SetupLoggedIn();
        SetupCreatedPost(OriginalMarkdown, isPublish: true);
        var captured = SetupUpdateAndCapture();
        _mockBlogPost.Setup(x => x.Get("post-1"))
            .ThrowsAsync(new Exception("Connection timeout"));

        var result = await PublishMarkdownAsync(OriginalMarkdown, publish: false);

        result.Success.Should().BeTrue();
        result.Post.Should().NotBeNull();
        result.Post!.Content.Should().Be(captured.Value);
        result.Post.Content.Should().NotBe("from-update-response");
        result.Post.IsPublish.Should().BeFalse();
        _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_GetReturnsEmptyContent_FallsBackToProcessedContent() {
        SetupLoggedIn();
        SetupCreatedPost(OriginalMarkdown, isPublish: true);
        var captured = SetupUpdateAndCapture();
        _mockBlogPost.Setup(x => x.Get("post-1"))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost { Id = "post-1", Content = "   ", IsPublish = false }
            });

        var result = await PublishMarkdownAsync(OriginalMarkdown, publish: false);

        result.Success.Should().BeTrue();
        result.Post!.Content.Should().Be(captured.Value);
        _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_GetReturnsNullData_FallsBackToProcessedContent() {
        SetupLoggedIn();
        SetupCreatedPost(OriginalMarkdown, isPublish: true);
        var captured = SetupUpdateAndCapture();
        _mockBlogPost.Setup(x => x.Get("post-1"))
            .ReturnsAsync(new ApiResponse<BlogPost> { Data = null, Message = "unavailable" });

        var result = await PublishMarkdownAsync(OriginalMarkdown, publish: false);

        result.Success.Should().BeTrue();
        result.Post!.Content.Should().Be(captured.Value);
        _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Success_AlwaysFetchesPostDetails() {
        SetupLoggedIn();
        SetupCreatedPost(OriginalMarkdown, isPublish: true);
        SetupUpdateAndCapture();
        _mockBlogPost.Setup(x => x.Get("post-1"))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost { Id = "post-1", Content = ServerRewrittenContent, IsPublish = true }
            });

        var result = await PublishMarkdownAsync(OriginalMarkdown, publish: true);

        result.Success.Should().BeTrue();
        _mockBlogPost.Verify(x => x.Get("post-1"), Times.Once);
    }

    // === GetPostAsync ===

    [Fact]
    public async Task GetPostAsync_Success_ReturnsPost() {
        SetupLoggedIn();

        var post = new BlogPost { Id = "123", Title = "Test Post" };
        _mockBlogPost.Setup(x => x.Get("123"))
            .ReturnsAsync(new ApiResponse<BlogPost> { Data = post });

        var result = await _publishService.GetPostAsync("123");

        result.Success.Should().BeTrue();
        result.Post!.Title.Should().Be("Test Post");
    }

    [Fact]
    public async Task GetPostAsync_NotFound_ReturnsFail() {
        SetupLoggedIn();

        _mockBlogPost.Setup(x => x.Get("999"))
            .ReturnsAsync(new ApiResponse<BlogPost> { Data = null, Message = "Not found" });

        var result = await _publishService.GetPostAsync("999");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not found");
    }

    [Fact]
    public async Task GetPostAsync_ApiThrows_ReturnsFail() {
        SetupLoggedIn();

        _mockBlogPost.Setup(x => x.Get(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Connection timeout"));

        var result = await _publishService.GetPostAsync("123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection timeout");
    }

    [Fact]
    public async Task GetPostAsync_NotLoggedIn_ReturnsFail() {
        var result = await _publishService.GetPostAsync("123");

        result.Success.Should().BeFalse();
    }
}
