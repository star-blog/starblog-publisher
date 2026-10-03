using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public class SitePostEditorViewModelTests {
    [Fact]
    public async Task SharedEditor_PreviewsUnsavedContentAndSavesThroughUpdate() {
        var (vm, posts, _) = CreateEditor();
        await vm.LoadAsync();
        vm.PostUrl = "https://example.com/p/site-post";
        vm.IsDirty.Should().BeFalse();
        vm.CanPreview.Should().BeTrue();
        vm.SelectedCategory!.Id.Should().Be(2);
        var editor = (IMarkdownEditorContext)vm;
        editor.SetEditorModeCommand.Execute("Split");
        vm.IsSplitMode.Should().BeTrue();
        vm.InspectorColumnWidth.Value.Should().Be(0);
        vm.IsDirty.Should().BeFalse();
        editor.ArticleContent = "# 未保存正文\n\n| A | B |\n| --- | --- |\n| 1 | 2 |";
        vm.Content.Should().Be(editor.ArticleContent);
        vm.IsDirty.Should().BeTrue();
        var html = await File.ReadAllTextAsync(vm.PreviewUri!.LocalPath);
        html.Should().Contain("未保存正文").And.Contain("<table ")
            .And.Contain("<base href=\"https://example.com/p/site-post\">");
        await vm.PublishCommand.ExecuteAsync(null);
        posts.Verify(p => p.Update("site-post", It.Is<PostUpdateDto>(dto => dto.Content == editor.ArticleContent && dto.CategoryId == 2 && dto.IsPublish)), Times.Once);
        vm.IsDirty.Should().BeFalse();
        editor.SetEditorModeCommand.Execute("Source");
        vm.InspectorColumnWidth.Value.Should().BeGreaterThanOrEqualTo(240);
    }

    [Fact]
    public async Task SitePreview_FollowsAppThemeAndClearsWhenContentIsEmpty() {
        var (vm, _, shell) = CreateEditor();
        await vm.LoadAsync();
        shell.SitePostsPage.OpenStackPage(vm, "编辑文章");
        shell.PreviewTheme(ThemeMode.Dark);
        var html = await File.ReadAllTextAsync(vm.PreviewUri!.LocalPath);
        html.Should().Contain("<body class=\"preview-dark\">");
        vm.Content = string.Empty;
        vm.PreviewUri.Should().BeNull();
        vm.CanPreview.Should().BeFalse();
        ((IMarkdownEditorContext)vm).SetEditorModeCommand.Execute("Preview");
        vm.IsSourceMode.Should().BeTrue();
        shell.PreviewTheme(ThemeMode.Light);
    }

    private static (SitePostEditorViewModel, Mock<IBlogPost>, MainWindowViewModel) CreateEditor() {
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false,
            workspaceHistoryPath: Path.Combine(Path.GetTempPath(), "starblog-site-editor-tests", Guid.NewGuid() + ".json"));
        var child = new Category { Id = 2, Text = "Child" };
        var categories = new Mock<ICategory>();
        categories.Setup(c => c.GetNodes()).ReturnsAsync(new ApiResponse<List<Category>> {
            Data = [new Category { Id = 1, Text = "Root", Nodes = [child] }]
        });
        var posts = new Mock<IBlogPost>();
        posts.Setup(p => p.Get("site-post")).ReturnsAsync(new ApiResponse<BlogPost> {
            Data = new BlogPost { Id = "site-post", Title = "Site post", Content = "# 原始正文", CategoryId = 2, IsPublish = true }
        });
        posts.Setup(p => p.Update("site-post", It.IsAny<PostUpdateDto>())).ReturnsAsync((string id, PostUpdateDto dto) =>
            new ApiResponse<BlogPost> { Data = new BlogPost { Id = id, Title = dto.Title, Content = dto.Content, CategoryId = dto.CategoryId, IsPublish = dto.IsPublish } });
        var api = new ApiService(Mock.Of<IAuth>(), posts.Object, categories.Object);
        var state = new GlobalState();
        state.SetLoggedIn("test-token");
        var auth = new AuthApplicationService(new AppSettings(), state, api);
        var vm = new SitePostEditorViewModel(shell, new ArticleLibraryApplicationService(api, auth), new CategoryApplicationService(api, auth), "site-post", _ => { });
        return (vm, posts, shell);
    }
}
