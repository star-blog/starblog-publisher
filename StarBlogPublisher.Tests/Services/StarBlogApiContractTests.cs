using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services;

/// <summary>Exercise the actual Refit clients with v1 HTTP payloads, rather than mocking the interfaces.</summary>
public class StarBlogApiContractTests {
    [Fact]
    public async Task Login_PostsCredentialsToTokens_AndReadsUnwrappedToken() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Post, "/api/v1/auth/tokens", """{"token":"jwt-v1","expiration":"2030-01-02T00:00:00Z"}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("username").GetString().Should().Be("alice");
                body.GetProperty("password").GetString().Should().Be("test-password");
                body.TryGetProperty("$type", out _).Should().BeFalse();
            });
        var state = new GlobalState();
        var auth = new AuthApplicationService(new AppSettings(), state, wire.Api);

        var result = await auth.LoginAsync("alice", "test-password");

        result.Success.Should().BeTrue();
        result.Token.Should().Be("jwt-v1");
        state.JwtToken.Should().Be("jwt-v1");
        wire.Complete();
    }

    [Fact]
    public async Task Login_ProblemDetails_ShowsServerReasonAndDoesNotLogIn() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Post, "/api/v1/auth/tokens",
            """{"title":"Unauthorized","status":401,"detail":"用户名或密码错误"}""", HttpStatusCode.Unauthorized);
        var state = new GlobalState();
        var result = await new AuthApplicationService(new AppSettings(), state, wire.Api).LoginAsync("alice", "bad");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401").And.Contain("用户名或密码错误").And.Contain("/api/v1/auth/tokens");
        state.IsLoggedIn.Should().BeFalse();
        wire.Complete();
    }

    [Fact]
    public async Task PostList_UsesV1FiltersAndMapsPaginationAndCategoryName() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/posts", """
            {"items":[{"id":"p1","title":"Title","categoryId":7,"categoryName":"开发","content":null}],
             "page":2,"pageSize":20,"totalCount":45,"totalPages":3}
            """, inspect: request => {
                var query = request.RequestUri!.Query;
                query.Should().Contain("page=2").And.Contain("pageSize=20").And.Contain("categoryId=7")
                    .And.Contain("isPublish=False").And.Contain("sortBy=Title").And.Contain("sortType=asc")
                    .And.Contain("includeDescendants=True").And.Contain("search=hello%20world");
                return Task.CompletedTask;
            });
        var result = await Library(wire.Api).ListAsync(new PostListQuery {
            Page = 2, PageSize = 20, CategoryId = 7, IsPublish = false,
            Search = "hello world", SortBy = "Title", SortType = "asc", IncludeDescendants = true
        });

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Posts.Should().ContainSingle();
        result.Posts[0].Category!.DisplayName.Should().Be("开发");
        result.Posts[0].Content.Should().BeNull();
        result.Pagination.PageNumber.Should().Be(2);
        result.Pagination.TotalItemCount.Should().Be(45);
        result.Pagination.TotalPages.Should().Be(3);
        result.Pagination.HasNextPage.Should().BeTrue();
        wire.Complete();
    }

    [Fact]
    public async Task PostUpdateAndDelete_ReadObjectAndAcceptNoContent() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Put, "/api/v1/posts/p1", """{"id":"p1","title":"Updated","content":"body","isPublish":false}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("title").GetString().Should().Be("Updated");
                body.GetProperty("isPublish").GetBoolean().Should().BeFalse();
                body.GetProperty("categoryId").GetInt32().Should().Be(7);
            });
        wire.Expect(HttpMethod.Delete, "/api/v1/posts/p1", status: HttpStatusCode.NoContent);
        var library = Library(wire.Api);

        var updated = await library.UpdateAsync("p1", new PostUpdateDto { Title = "Updated", Content = "body", CategoryId = 7 });
        var deleted = await library.DeleteAsync("p1");

        updated.Success.Should().BeTrue();
        updated.PostUrl.Should().EndWith("/blog/p1");
        deleted.Success.Should().BeTrue();
        wire.Complete();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Publish_CreatesDraft_UploadsMultipart_ThenUpdatesFinalState(bool publish) {
        using var wire = new Wire();
        const string uploaded = "https://blog.example/media/blog/p1/cover.png";
        wire.Expect(HttpMethod.Post, "/api/v1/posts", """{"id":"p1","title":"Title","content":"![cover](cover.png)","isPublish":false,"categoryId":7}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("isPublish").GetBoolean().Should().BeFalse();
                body.TryGetProperty("zipEncoding", out _).Should().BeFalse();
            });
        wire.Expect(HttpMethod.Post, "/api/v1/posts/p1/images", """{"url":"https://blog.example/media/blog/p1/cover.png"}""",
            inspect: async request => {
                request.Content!.Headers.ContentType!.MediaType.Should().Be("multipart/form-data");
                var payload = await request.Content.ReadAsStringAsync();
                payload.Should().Contain("filename=").And.Contain("cover").And.Contain("image-bytes");
            });
        wire.Expect(HttpMethod.Put, "/api/v1/posts/p1",
            $$"""{"id":"p1","title":"Title","content":"![cover]({{uploaded}})","isPublish":{{publish.ToString().ToLowerInvariant()}},"categoryId":7}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("isPublish").GetBoolean().Should().Be(publish);
                body.GetProperty("content").GetString().Should().Contain(uploaded).And.NotContain("](cover.png)");
                body.GetProperty("slug").GetString().Should().Be("title");
            });
        wire.Expect(HttpMethod.Get, "/api/v1/posts/p1",
            $$"""{"id":"p1","title":"Title","content":"![cover]({{uploaded}})","isPublish":{{publish.ToString().ToLowerInvariant()}},"slug":"title","categoryId":7,"categoryName":"开发"}""");
        var settings = new AppSettings();
        var publisher = new ArticlePublishApplicationService(wire.Api, LoggedIn(wire.Api), settings);
        var directory = Path.Combine(Path.GetTempPath(), "starblog-api-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            await File.WriteAllTextAsync(Path.Combine(directory, "cover.png"), "image-bytes");
            var result = await publisher.PublishAsync(Path.Combine(directory, "post.md"), "Title", "![cover](cover.png)", "summary", 7, "title", publish);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.Post!.IsPublish.Should().Be(publish);
            result.MarkdownContent.Should().Contain(uploaded);
            result.PostUrl.Should().EndWith("/p/title");
            wire.Complete();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CategoryTreeAndWordCloud_ReadRawArraysAndCount() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/categories/tree", """[{"id":1,"text":"Root","tags":["2"],"nodes":[{"id":2,"text":"Child"}]}]""");
        wire.Expect(HttpMethod.Get, "/api/v1/categories/word-cloud", """[{"id":1,"name":"Root","count":2}]""");

        var categories = await new CategoryApplicationService(wire.Api, LoggedIn(wire.Api)).GetCategoriesAsync();
        var words = await wire.Api.Categories.GetWordCloud();

        categories.Success.Should().BeTrue();
        categories.Categories![0].Nodes![0].DisplayName.Should().Be("Child");
        words.Should().ContainSingle().Which.Count.Should().Be(2);
        wire.Complete();
    }

    [Fact]
    public async Task CategoryCreateDelete_UseRawObjectAndNoContent() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Post, "/api/v1/categories", """{"id":8,"name":"New","parentId":2,"visible":true}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("name").GetString().Should().Be("New");
                body.GetProperty("parentId").GetInt32().Should().Be(2);
            });
        wire.Expect(HttpMethod.Delete, "/api/v1/categories/8", status: HttpStatusCode.NoContent);
        var service = new CategoryApplicationService(wire.Api, LoggedIn(wire.Api));

        (await service.CreateCategoryAsync("New", 2)).Categories.Should().ContainSingle(c => c.Id == 8);
        (await service.DeleteCategoryAsync(8)).Success.Should().BeTrue();
        wire.Complete();
    }

    [Fact]
    public async Task CategoryVisibility_LoadsAllPagesIncludingHidden_AndPreservesOtherFields() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/categories", """{"items":[{"id":1,"name":"Root"}],"page":1,"pageSize":1,"totalCount":2,"totalPages":2}""",
            inspect: request => {
                request.RequestUri!.Query.Should().Contain("page=1");
                return Task.CompletedTask;
            });
        wire.Expect(HttpMethod.Get, "/api/v1/categories", """{"items":[{"id":8,"name":"Hidden","parentId":1,"visible":false}],"page":2,"pageSize":1,"totalCount":2,"totalPages":2}""",
            inspect: request => {
                request.RequestUri!.Query.Should().Contain("page=2");
                return Task.CompletedTask;
            });
        wire.Expect(HttpMethod.Put, "/api/v1/categories/8", """{"id":8,"name":"Hidden","parentId":1,"visible":true}""",
            inspect: async request => {
                var body = await Body(request);
                body.GetProperty("name").GetString().Should().Be("Hidden");
                body.GetProperty("parentId").GetInt32().Should().Be(1);
                body.GetProperty("visible").GetBoolean().Should().BeTrue();
            });
        var service = new CategoryApplicationService(wire.Api, LoggedIn(wire.Api));

        var result = await service.SetVisibilityAsync(8, true);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Category!.Visible.Should().BeTrue();
        wire.Complete();
    }

    [Fact]
    public async Task EmptyCategoryTree_NoContent_IsSuccessfulEmptyList() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/categories/tree", status: HttpStatusCode.NoContent);

        var result = await new CategoryApplicationService(wire.Api, LoggedIn(wire.Api)).GetCategoriesAsync();

        result.Success.Should().BeTrue();
        result.Categories.Should().BeEmpty();
        wire.Complete();
    }

    [Fact]
    public async Task FailedDelete_ShowsProblemDetails() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Delete, "/api/v1/posts/p1", """{"status":409,"title":"Conflict","detail":"文章仍被引用"}""", HttpStatusCode.Conflict);

        var result = await Library(wire.Api).DeleteAsync("p1");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("409").And.Contain("文章仍被引用");
        wire.Complete();
    }

    private static AuthApplicationService LoggedIn(ApiService api) {
        var state = new GlobalState();
        state.SetLoggedIn("test-token");
        return new AuthApplicationService(new AppSettings(), state, api);
    }

    [Theory]
    [InlineData("cover.jpg", "https://blog.example/media/photography/cover.jpg")]
    [InlineData("/cover.jpg", "https://blog.example/media/photography/cover.jpg")]
    [InlineData("album\\cover.jpg", "https://blog.example/media/photography/album/cover.jpg")]
    public async Task RandomPhoto_ResolvesV1MetadataToDownloadUrl(string filePath, string expected) {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/site/photos/random", JsonSerializer.Serialize(new { id = "photo1", filePath }));

        var uri = await new SitePhotoApplicationService(wire.Api).GetRandomImageUriAsync();

        uri.AbsoluteUri.Should().Be(expected);
        wire.Complete();
    }

    [Fact]
    public async Task RandomPhoto_MetadataRequest_PropagatesCancellation() {
        using var wire = new Wire();
        wire.Expect(HttpMethod.Get, "/api/v1/site/photos/random", inspect: async _ =>
            await Task.Delay(Timeout.Infinite, wire.LastCancellationToken));
        using var cancellation = new CancellationTokenSource();
        var pending = new SitePhotoApplicationService(wire.Api).GetRandomImageUriAsync(cancellation.Token);

        cancellation.Cancel();

        await FluentActions.Awaiting(() => pending).Should().ThrowAsync<OperationCanceledException>();
        wire.Complete();
    }

    private static ArticleLibraryApplicationService Library(ApiService api) => new(api, LoggedIn(api));
    private static async Task<JsonElement> Body(HttpRequestMessage request) =>
        JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone();

    private sealed class Wire : HttpMessageHandler {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _expected = new();
        private readonly HttpClient _client;
        public ApiService Api { get; }
        public CancellationToken LastCancellationToken { get; private set; }
        public Wire() {
            _client = new HttpClient(this, disposeHandler: false) { BaseAddress = new Uri("https://blog.example") };
            Api = new ApiService(_client);
        }

        public void Expect(HttpMethod method, string path, string? json = null,
            HttpStatusCode status = HttpStatusCode.OK, Func<HttpRequestMessage, Task>? inspect = null) {
            _expected.Enqueue(async request => {
                request.Method.Should().Be(method);
                request.RequestUri!.AbsolutePath.Should().Be(path);
                if (inspect != null) await inspect(request);
                return new HttpResponseMessage(status) {
                    RequestMessage = request,
                    Content = json == null ? null : new StringContent(json, Encoding.UTF8,
                        (int)status >= 400 ? "application/problem+json" : "application/json")
                };
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            LastCancellationToken = cancellationToken;
            _expected.Should().NotBeEmpty($"unexpected request: {request.Method} {request.RequestUri}");
            return _expected.Dequeue()(request);
        }
        public void Complete() => _expected.Should().BeEmpty("all expected HTTP calls must be exercised");
        protected override void Dispose(bool disposing) {
            if (disposing) _client.Dispose();
            base.Dispose(disposing);
        }
    }
}
