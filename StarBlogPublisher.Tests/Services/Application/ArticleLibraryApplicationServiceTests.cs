using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;

namespace StarBlogPublisher.Tests.Services.Application;

public class ArticleLibraryApplicationServiceTests {
    private readonly AppSettings _settings;
    private readonly Mock<IBlogPost> _mockBlogPost;
    private readonly ArticleLibraryApplicationService _library;

    public ArticleLibraryApplicationServiceTests() {
        _settings = new AppSettings();
        var globalState = new GlobalState();
        var mockAuth = new Mock<IAuth>();
        _mockBlogPost = new Mock<IBlogPost>();
        var api = new ApiService(mockAuth.Object, _mockBlogPost.Object, Mock.Of<ICategory>());
        var authService = new AuthApplicationService(_settings, globalState, api);
        _library = new ArticleLibraryApplicationService(api, authService);
        _settings.Username = "user";
        _settings.Password = "pass";
        mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> { Data = new LoginToken { Token = "token" } });
        authService.LoginAsync("user", "pass").Wait();
    }

    [Fact]
    public async Task ListAsync_ReturnsPagedPosts() {
        _mockBlogPost.Setup(x => x.GetList(It.IsAny<PostListQuery>()))
            .ReturnsAsync(new ApiResponsePaged<BlogPost> {
                Data = [new BlogPost { Id = "p1", Title = "Hello", IsPublish = true }],
                Pagination = new PaginationMetadata { PageNumber = 1, PageSize = 20, TotalItemCount = 1, PageCount = 1 }
            });

        var result = await _library.ListAsync(new PostListQuery { Page = 1 });

        result.Success.Should().BeTrue();
        result.Posts.Should().ContainSingle(p => p.Id == "p1");
        result.Pagination.TotalItemCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_EmptyTitle_ReturnsFail() {
        var result = await _library.UpdateAsync("p1", new PostUpdateDto {
            Title = "",
            Content = "body",
            CategoryId = 1
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("标题");
    }

    [Fact]
    public async Task UpdateAsync_CallsApiAndReturnsPost() {
        _mockBlogPost.Setup(x => x.Update("p1", It.IsAny<PostUpdateDto>()))
            .ReturnsAsync((string id, PostUpdateDto dto) => new ApiResponse<BlogPost> {
                Data = new BlogPost { Id = id, Title = dto.Title, Content = dto.Content, IsPublish = dto.IsPublish }
            });

        var result = await _library.UpdateAsync("p1", new PostUpdateDto {
            Title = "Updated",
            Content = "body",
            CategoryId = 2,
            IsPublish = false
        });

        result.Success.Should().BeTrue();
        result.Post!.Title.Should().Be("Updated");
        result.Post.IsPublish.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_UsesApiMessage() {
        _mockBlogPost.Setup(x => x.Delete("p1"))
            .ReturnsAsync(new ApiResponse<object> { Successful = true, StatusCode = 200, Message = "deleted 1" });

        var result = await _library.DeleteAsync("p1");

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("deleted");
    }

    [Fact]
    public async Task PullAsync_WritesMarkdownAndSidecar() {
        _mockBlogPost.Setup(x => x.Get("p1"))
            .ReturnsAsync(new ApiResponse<BlogPost> {
                Data = new BlogPost {
                    Id = "p1",
                    Title = "Pulled",
                    Summary = "sum",
                    Slug = "pulled",
                    Content = "# Pulled",
                    CategoryId = 3,
                    Category = new Category { Id = 3, Text = "Notes" },
                    IsPublish = true
                }
            });
        var directory = Path.Combine(Path.GetTempPath(), "starblog-pull-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "pulled.md");

        try {
            var result = await _library.PullAsync("p1", path);

            result.Success.Should().BeTrue();
            (await File.ReadAllTextAsync(path)).Should().Be("# Pulled");
            var sidecar = await ArticleSidecar.ReadAsync(path);
            sidecar.Should().NotBeNull();
            sidecar!.PostId.Should().Be("p1");
            sidecar.Title.Should().Be("Pulled");
            sidecar.IsPublish.Should().BeTrue();
        }
        finally {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
