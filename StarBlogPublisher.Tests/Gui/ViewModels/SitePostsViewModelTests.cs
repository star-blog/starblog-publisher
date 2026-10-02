using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
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

        await vm.RefreshAsync();

        vm.Posts.Should().BeEmpty();
        vm.StatusMessage.Should().Contain("登录");
        vm.IsBusy.Should().BeFalse();
    }

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
