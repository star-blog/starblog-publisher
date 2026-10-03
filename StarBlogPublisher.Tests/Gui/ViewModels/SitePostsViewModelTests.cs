using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public class SitePostsViewModelTests {
    private static MainWindowViewModel Shell() => new(Mock.Of<IHttpClientFactory>(), initializeSession: false,
        workspaceHistoryPath: Path.Combine(Path.GetTempPath(), "starblog-site-posts-tests", Guid.NewGuid() + ".json"));

    [Fact]
    public void Constructor_ExposesDefaultFilters() {
        var vm = Shell().SitePostsPage;

        vm.Title.Should().Be("站点文章");
        vm.StatusFilters.Should().HaveCount(3);
        vm.SelectedStatusFilter!.Id.Should().Be("all");
        vm.CategoryFilters.Should().ContainSingle(c => c.Id == 0);
        vm.HasSelectedPost.Should().BeFalse();
        vm.PageSummary.Should().Contain("没有文章");
    }

    [Fact]
    public async Task Refresh_WhenLoggedOut_LeavesEmptyList() {
        var vm = Shell().SitePostsPage;
        vm.Posts.Add(new SitePostListItem(new BlogPost { Id = "old", Title = "Old" }));
        vm.SelectedPost = vm.Posts[0];

        await vm.RefreshAsync();

        vm.Posts.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("登录");
        vm.IsBusy.Should().BeFalse();
        vm.SelectedPost.Should().BeNull();
        vm.NeedsLogin.Should().BeTrue();
        vm.ShowEmptyState.Should().BeTrue();
        vm.EmptyTitle.Should().Contain("登录");
    }

    [Fact]
    public async Task FilterChangedDuringLoad_UsesLatestQueryAndSkipsStaleRows() {
        var posts = new Mock<IBlogPost>();
        var first = new TaskCompletionSource<ApiResponsePaged<BlogPost>>();
        posts.SetupSequence(x => x.GetList(It.IsAny<PostListQuery>()))
            .Returns(first.Task)
            .ReturnsAsync(PageResult("draft", 1, 1));
        var vm = LoggedInLibrary(posts);
        try {
            vm.HasLoaded = true;
            var loading = vm.RefreshAsync();
            vm.SelectedStatusFilter = vm.StatusFilters[2];
            first.SetResult(PageResult("stale", 1, 1));
            await loading;

            vm.Posts.Should().ContainSingle(p => p.Id == "draft");
            posts.Verify(x => x.GetList(It.Is<PostListQuery>(q => q.IsPublish == false && q.Page == 1)), Times.Once);
            vm.IsBusy.Should().BeFalse();
        }
        finally { GlobalState.Instance.Logout(); }
    }

    [Fact]
    public async Task Refresh_WhenPageRemoved_LoadsActualLastPage() {
        var posts = new Mock<IBlogPost>();
        posts.SetupSequence(x => x.GetList(It.IsAny<PostListQuery>()))
            .ReturnsAsync(PageResult(null, 1, 1))
            .ReturnsAsync(PageResult("last", 1, 1));
        var vm = LoggedInLibrary(posts);
        try {
            vm.Page = 2;
            await vm.RefreshAsync();
            vm.Page.Should().Be(1);
            vm.Posts.Should().ContainSingle(p => p.Id == "last");
            posts.Verify(x => x.GetList(It.Is<PostListQuery>(q => q.Page == 1)), Times.Once);
        }
        finally { GlobalState.Instance.Logout(); }
    }

    [Fact]
    public async Task FailedLoad_ShowsFailureAndClearsStaleSelection() {
        var posts = new Mock<IBlogPost>();
        posts.Setup(x => x.GetList(It.IsAny<PostListQuery>())).ThrowsAsync(new IOException("offline"));
        var vm = LoggedInLibrary(posts);
        try {
            vm.Posts.Add(new SitePostListItem(new BlogPost { Id = "old" }));
            vm.SelectedPost = vm.Posts[0];
            await vm.RefreshAsync();
            vm.HasLoadError.Should().BeTrue();
            vm.EmptyDescription.Should().Contain("offline");
            vm.HasSelectedPost.Should().BeFalse();
            vm.ShowEmptyState.Should().BeTrue();
        }
        finally { GlobalState.Instance.Logout(); }
    }

    [Fact]
    public async Task ClearFilters_ResetsAllFiltersWithOneRequest() {
        var posts = new Mock<IBlogPost>();
        posts.Setup(x => x.GetList(It.IsAny<PostListQuery>())).ReturnsAsync(PageResult(null, 0, 0));
        var vm = LoggedInLibrary(posts);
        try {
            vm.SearchText = "keyword";
            vm.SelectedStatusFilter = vm.StatusFilters[2];
            vm.SelectedCategoryFilter = new Category { Id = 2, Text = "Dev" };
            vm.Page = 3;
            vm.HasLoaded = true;
            await vm.ClearFiltersCommand.ExecuteAsync(null);
            vm.HasActiveFilters.Should().BeFalse();
            vm.Page.Should().Be(1);
            posts.Verify(x => x.GetList(It.Is<PostListQuery>(q => q.Search == null && q.IsPublish == null && q.CategoryId == 0 && q.Page == 1)), Times.Once);
        }
        finally { GlobalState.Instance.Logout(); }
    }

    [Fact]
    public async Task LoadFilters_PreservesTreeAndRestoresNestedSelection() {
        var child = new Category { Id = 2, Text = "Child" };
        var root = new Category { Id = 1, Text = "Root", Nodes = [child] };
        var categories = new Mock<ICategory>();
        categories.Setup(x => x.GetNodes()).ReturnsAsync(new ApiResponse<List<Category>> { Data = [root] });
        var vm = LoggedInLibrary(new Mock<IBlogPost>(), categories);
        try {
            vm.SelectedCategoryFilter = new Category { Id = 2, Text = "Old child" };
            await vm.LoadFiltersAsync();
            vm.CategoryFilters.Should().HaveCount(2); // All-categories entry plus one tree root.
            vm.CategoryFilters[1].Nodes.Should().ContainSingle().Which.Should().BeSameAs(child);
            vm.SelectedCategoryFilter.Should().BeSameAs(child);
            vm.CategorySelectionText.Should().Be("Child");
            vm.IsLoadingCategories.Should().BeFalse();
        }
        finally { GlobalState.Instance.Logout(); }
    }

    private static SitePostsViewModel LoggedInLibrary(Mock<IBlogPost> posts, Mock<ICategory>? categories = null) {
        var shell = Shell();
        GlobalState.Instance.SetLoggedIn("test-token");
        var state = new GlobalState();
        state.SetLoggedIn("test-token");
        var api = new ApiService(Mock.Of<IAuth>(), posts.Object, categories?.Object ?? Mock.Of<ICategory>());
        var auth = new AuthApplicationService(new AppSettings(), state, api);
        return new SitePostsViewModel(shell, new ArticleLibraryApplicationService(api, auth), new CategoryApplicationService(api, auth));
    }

    private static ApiResponsePaged<BlogPost> PageResult(string? id, int total, int pages) => new() {
        Data = id == null ? [] : [new BlogPost { Id = id, Title = id }],
        Pagination = new PaginationMetadata { TotalItemCount = total, PageCount = pages, PageNumber = 1, PageSize = 20 }
    };

    [Fact]
    public void ListItem_MapsStatusAndCategory() {
        var item = new SitePostListItem(new BlogPost {
            Id = "p1",
            Title = "Hello",
            IsPublish = false,
            Slug = "hello",
            Category = new Category { Id = 1, Text = "Dev" },
            LastUpdateTime = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc)
        });

        item.StatusText.Should().Be("草稿");
        item.CategoryName.Should().Be("Dev");
        item.Slug.Should().Be("hello");
    }

    [Fact]
    public void CommandPalette_IncludesSitePosts() {
        var shell = Shell();
        shell.GetCommand("view.sitePosts").Label.Should().Be("站点文章");
        shell.NavigationItems.Select(i => i.Title).Should().Contain("站点文章");
    }
}
