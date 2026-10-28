using System.Reflection;
using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public class WeChatViewModelTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "starblog-wechat-tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public void Constructor_SelectsUsableDefaults() {
        var viewModel = CreateViewModel();

        viewModel.Themes.Should().NotBeEmpty();
        viewModel.SelectedTheme.Should().NotBeNull();
        viewModel.ConvertExternalLinksToReferences.Should().BeTrue();
        viewModel.InsertAccountChrome.Should().BeTrue();
        viewModel.SelectedCoverSource!.Id.Should().Be("random");
        viewModel.SelectedCoverSize!.Id.Should().Be("headline");
        viewModel.SelectedRandomCoverProvider.Should().NotBeNull();
        viewModel.IsRandomCoverSource.Should().BeTrue();
        viewModel.IsLocalCoverSource.Should().BeFalse();
        viewModel.IsHeadlineCoverSize.Should().BeTrue();
        viewModel.CoverPreviewHeight.Should().BeApproximately(122.56, 0.01);
    }

    [Fact]
    public void SetCoverSourceCommand_ChangesTheActiveSource() {
        var viewModel = CreateViewModel();

        viewModel.SetCoverSourceCommand.Execute("url");

        viewModel.SelectedCoverSource!.Id.Should().Be("url");
        viewModel.IsUrlCoverSource.Should().BeTrue();
        viewModel.IsLocalCoverSource.Should().BeFalse();
        viewModel.IsRandomCoverSource.Should().BeFalse();
    }

    [Fact]
    public void SetCoverSizeCommand_ChangesThePreviewDimensions() {
        var viewModel = CreateViewModel();

        viewModel.SetCoverSizeCommand.Execute("secondary");

        viewModel.SelectedCoverSize!.Id.Should().Be("secondary");
        viewModel.IsSecondaryCoverSize.Should().BeTrue();
        viewModel.IsHeadlineCoverSize.Should().BeFalse();
        viewModel.CoverPreviewHeight.Should().Be(192);
    }

    [Fact]
    public void ToggleInspectorCommand_SwitchesTheInspectorPaneWidth() {
        var viewModel = CreateViewModel();

        viewModel.ToggleInspectorCommand.Execute(null);

        viewModel.IsInspectorOpen.Should().BeFalse();
        viewModel.InspectorPaneWidth.Should().Be(48);

        viewModel.ToggleInspectorCommand.Execute(null);

        viewModel.IsInspectorOpen.Should().BeTrue();
        viewModel.InspectorPaneWidth.Should().Be(320);
    }

    [Fact]
    public void SectionExpansion_UsesCompactSidebarDefaults() {
        var viewModel = CreateViewModel();

        viewModel.IsThemeSectionExpanded.Should().BeTrue();
        viewModel.IsBasicSectionExpanded.Should().BeTrue();
        viewModel.IsCoverSectionExpanded.Should().BeTrue();
        viewModel.IsSourceSectionExpanded.Should().BeFalse();
        viewModel.IsHtmlSectionExpanded.Should().BeFalse();
    }

    [Fact]
    public void ArticleTitle_IsClampedToWeChatLimit() {
        var viewModel = CreateViewModel();

        viewModel.ArticleTitle = new string('题', 80);

        viewModel.ArticleTitle.Should().HaveLength(64);
        viewModel.TitleCounterText.Should().Be("64/64");
    }

    [Fact]
    public void ArticleTitle_CountsEnglishAsHalfWords() {
        var viewModel = CreateViewModel();

        viewModel.ArticleTitle = new string('A', 127);
        viewModel.TitleCounterText.Should().Be("63.5/64");

        viewModel.ArticleTitle += "BC";
        viewModel.ArticleTitle.Should().Be(new string('A', 127) + "B");
        viewModel.TitleCounterText.Should().Be("64/64");
    }

    [Fact]
    public void ArticleTitle_DoesNotSplitEmojiWhenClamped() {
        var viewModel = CreateViewModel();

        viewModel.ArticleTitle = new string('题', 63) + "\U0001F600";

        viewModel.ArticleTitle.Should().Be(new string('题', 63));
        viewModel.TitleCounterText.Should().Be("63/64");
    }

    [Fact]
    public void Author_IsClampedToWeChatLimit() {
        var viewModel = CreateViewModel();

        viewModel.Author = new string('作', 20);

        viewModel.Author.Should().HaveLength(16);
        viewModel.AuthorCounterText.Should().Be("16/16");
    }

    [Fact]
    public void Comments_DefaultToEveryoneAndClearFansOnlyWhenClosed() {
        var viewModel = CreateViewModel();

        viewModel.OpenComment.Should().BeTrue();
        viewModel.FansOnlyComment.Should().BeFalse();

        viewModel.FansOnlyComment = true;
        viewModel.OpenComment = false;

        viewModel.FansOnlyComment.Should().BeFalse();
    }

    [Fact]
    public void GenerateFormat_InsertsTheSelectedAccountChromeUntilTurnedOff() {
        var viewModel = CreateViewModel();
        var previousTheme = AppSettings.Instance.WeChatDefaultTheme;
        try {
            viewModel.SelectedWeChatAccount.Should().NotBeNull();
            viewModel.SelectedWeChatAccount!.HeaderHtml = "<p>文章开头标记</p>";
            viewModel.SelectedWeChatAccount.FooterHtml = "<p>文末引导标记</p><mp-common-profile data-nickname=\"曦远Life\"></mp-common-profile>";
            viewModel.GetType().GetField("_markdown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewModel, "# 标题\n\n正文");
            viewModel.HasArticle = true;

            viewModel.GenerateFormatCommand.Execute(null);

            viewModel.FormattedHtml.Should().Contain("文章开头标记");
            viewModel.FormattedHtml.Should().Contain("文末引导标记");
            viewModel.FormattedHtml.Should().Contain("mp-common-profile");
            viewModel.StatusMessage.Should().Contain("已插入开头和文末");
            var wordCount = viewModel.WordCount;

            viewModel.InsertAccountChrome = false;

            viewModel.FormattedHtml.Should().NotContain("文章开头标记");
            viewModel.FormattedHtml.Should().NotContain("mp-common-profile");
            viewModel.WordCount.Should().Be(wordCount);
        }
        finally {
            AppSettings.Instance.WeChatDefaultTheme = previousTheme;
        }
    }

    [Fact]
    public void GenerateFormat_PreservesAnEditedTitle() {
        var viewModel = CreateViewModel();
        var previousTheme = AppSettings.Instance.WeChatDefaultTheme;
        try {
            viewModel.GetType().GetField("_markdown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewModel, "# 原文标题\n\n正文内容");
            viewModel.HasArticle = true;

            viewModel.GenerateFormatCommand.Execute(null);
            viewModel.ArticleTitle.Should().Be("原文标题");

            viewModel.ArticleTitle = "自定义标题";
            viewModel.GenerateFormatCommand.Execute(null);

            viewModel.ArticleTitle.Should().Be("自定义标题");
            viewModel.FormattedHtml.Should().NotBeNullOrWhiteSpace();
        }
        finally {
            AppSettings.Instance.WeChatDefaultTheme = previousTheme;
        }
    }

    [Fact]
    public async Task ExternalLinkReferences_RefreshPreviewWhenToggledWithoutChangingMarkdown() {
        const string markdown = "# 标题\n\n[外部文章](https://example.com/article)";
        var path = await Article("references.md", markdown);
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false,
            workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));
        await shell.Workspace.OpenPathAsync(path);
        var viewModel = CreateViewModel();
        var previousTheme = AppSettings.Instance.WeChatDefaultTheme;
        try {
            viewModel.SyncFrom(shell.PublishPage);
            viewModel.ArticleTitle = "自定义标题";
            viewModel.FormattedHtml.Should().Contain("引用链接");
            var wordCount = viewModel.WordCount;

            viewModel.ConvertExternalLinksToReferences = false;

            viewModel.FormattedHtml.Should().Contain("href=\"https://example.com/article\"");
            viewModel.FormattedHtml.Should().NotContain("引用链接");

            viewModel.ConvertExternalLinksToReferences = true;
            viewModel.SelectedTheme = WeChatThemeCatalog.Resolve("warm-card");
            viewModel.GenerateFormatCommand.Execute(null);

            viewModel.FormattedHtml.Should().Contain(">[1]</sup>");
            viewModel.FormattedHtml.Should().NotContain(">[2]</sup>");
            viewModel.FormattedHtml.Should().Contain("引用链接");
            viewModel.WordCount.Should().Be(wordCount);
            viewModel.ArticleTitle.Should().Be("自定义标题");
            shell.PublishPage.ArticleContent.Should().Be(markdown);
            File.ReadAllText(path).Should().Be(markdown);
        }
        finally {
            AppSettings.Instance.WeChatDefaultTheme = previousTheme;
        }
    }

    [Fact]
    public void Digest_IsClampedToWeChatLimit() {
        var viewModel = CreateViewModel();

        viewModel.Digest = new string('摘', 150);

        viewModel.Digest.Should().HaveLength(120);
        viewModel.DigestCounterText.Should().Be("120/120");
        viewModel.DigestRemainingLength.Should().Be(0);
    }

    [Fact]
    public void Digest_RegressionSampleMatchesWeChatCounterAndPreservesHalfWords() {
        const string summary = "C# 代码长到像答辩？用 CSharpier 自动折行，再用 Roslyn 修正 K&R 花括号，Taskfile 一键收尾。再补上 format-check，省得 CI 反复制造 Git diff。两步流水线并非原生 gofmt，顺序和版";
        var viewModel = CreateViewModel();

        viewModel.Digest = summary;

        viewModel.Digest.Should().Be(summary);
        viewModel.DigestCounterText.Should().Be("86/120");
        viewModel.DigestRemainingLength.Should().Be(34m);

        viewModel.Digest += "a";

        viewModel.Digest.Should().Be(summary + "a");
        viewModel.DigestCounterText.Should().Be("86.5/120");
        viewModel.DigestRemainingLength.Should().Be(33.5m);
    }

    [Fact]
    public void Digest_Allows240EnglishCharactersAndClampsAtTheWeightedLimit() {
        var viewModel = CreateViewModel();

        viewModel.Digest = new string('a', 241);

        viewModel.Digest.Should().Be(new string('a', 240));
        viewModel.DigestCounterText.Should().Be("120/120");
        viewModel.DigestRemainingLength.Should().Be(0m);

        viewModel.Digest = new string('a', 239) + "中b";

        viewModel.Digest.Should().Be(new string('a', 239));
        viewModel.DigestCounterText.Should().Be("119.5/120");
        viewModel.DigestRemainingLength.Should().Be(0.5m);
    }

    [Fact]
    public async Task UseCoverUrlCommand_InvalidUrl_DoesNotStartAnExternalRequest() {
        var viewModel = CreateViewModel();
        viewModel.CoverUrl = "not a URL";

        await viewModel.UseCoverUrlCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Contain("HTTP");
        viewModel.IsPreparingCover.Should().BeFalse();
        viewModel.CoverPath.Should().BeEmpty();
    }

    [Fact]
    public void RandomCoverProvider_CreateUri_FormatsDimensionsAndNonce() {
        var provider = new RandomCoverProvider("Test", "https://images.example/{0}/{1}?random={2}");

        var uri = provider.CreateUri(900, 383, 12345);

        uri.ToString().Should().Be("https://images.example/900/383?random=12345");
    }

    [Fact]
    public void ShowCoverStudio_OpensComposerWithTheArticleTitle() {
        var viewModel = CreateViewModel();
        viewModel.ArticleTitle = "封面标题";

        viewModel.ShowCoverStudioCommand.Execute(null);

        viewModel.IsStackNavigating.Should().BeTrue();
        viewModel.Breadcrumbs.Select(item => item.Title).Should().Equal("公众号排版", "制作封面");
        var studio = viewModel.ActiveStackPage.Should().BeOfType<CoverStudioViewModel>().Subject;
        studio.CoverTitle.Should().Be("封面标题");
        studio.ShowScrim.Should().BeTrue();
        studio.IsBold.Should().BeTrue();
        studio.IsBottomPlacement.Should().BeTrue();
        studio.IsCenterAlign.Should().BeTrue();

        viewModel.NavigateBreadcrumbAt(0);

        viewModel.IsStackNavigating.Should().BeFalse();
        viewModel.ActiveStackPage.Should().BeNull();
    }

    [Fact]
    public void ShowCoverStudio_UsesCurrentArticleImagesAndResolvesRelativePaths() {
        var viewModel = CreateViewModel();
        var article = Path.Combine(Path.GetTempPath(), "article", "post.md");
        typeof(WeChatViewModel).GetField("_sourceFilePath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, article);
        viewModel.FormattedHtml = "<img src='images/photo.png' alt='配图'><img src='https://images.example/photo.png'>";

        viewModel.ShowCoverStudioCommand.Execute(null);

        var studio = viewModel.ActiveStackPage.Should().BeOfType<CoverStudioViewModel>().Subject;
        studio.ArticleImages.Select(image => image.Source).Should().Equal(
            Path.Combine(Path.GetDirectoryName(article)!, "images", "photo.png"), "https://images.example/photo.png");
        studio.HasArticleImages.Should().BeTrue();
        viewModel.NavigateBreadcrumbAt(0);

        viewModel.FormattedHtml = "<p>另一篇没有图片的文章</p>";
        viewModel.ShowCoverStudioCommand.Execute(null);
        var next = viewModel.ActiveStackPage.Should().BeOfType<CoverStudioViewModel>().Subject;
        next.HasArticleImages.Should().BeFalse();
        viewModel.NavigateBreadcrumbAt(0);
    }

    [Fact]
    public async Task UsePreparedCover_InstallsTheComposedImage() {
        var viewModel = CreateViewModel();
        var composed = await new CoverComposer().ComposeAsync(null, new CoverComposition { Title = "" });
        try {
            viewModel.UsePreparedCover(
                new PreparedWeChatCover(composed.Path, composed.Width, composed.Height),
                "制作封面 · 1200 × 900");

            viewModel.CoverPath.Should().Be(composed.Path);
            viewModel.CoverPixelWidth.Should().Be(1200);
            viewModel.CoverPixelHeight.Should().Be(900);
            viewModel.HasCover.Should().BeTrue();
            viewModel.CoverSourceDescription.Should().Be("制作封面 · 1200 × 900");
            viewModel.StatusMessage.Should().Contain("制作的封面");
        }
        finally {
            viewModel.CoverPreview?.Dispose();
            File.Delete(composed.Path);
        }
    }

    [Fact]
    public async Task SyncFrom_AfterStarBlogPublish_KeepsLocalImagePathsInsteadOfBlogUrls() {
        var path = await Article("post.md", "# 标题\n\n![封面](images/cover.png)\n\n正文");
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false,
            workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));
        await shell.Workspace.OpenPathAsync(path);
        var publish = shell.PublishPage;
        publish.LastPublishResult = PublishResult.Ok(
            new BlogPost { Id = "post-1", Title = "标题", Content = "# 标题\n\n![封面](https://blog.example/media/cover.png)\n\n正文" },
            "https://blog.example/p/post",
            "# 标题\n\n![封面](https://blog.example/media/cover.png)\n\n正文");

        var viewModel = CreateViewModel();
        viewModel.SyncFrom(publish);

        publish.ArticleContent.Should().Contain("images/cover.png");
        viewModel.MarkdownSourceMessage.Should().Contain("本地 Markdown 原文");
        viewModel.ContentSourceUrl.Should().Be("https://blog.example/p/post");
        viewModel.FormattedHtml.Should().Contain("images/cover.png");
        viewModel.FormattedHtml.Should().NotContain("blog.example/media/cover.png");
    }

    [Fact]
    public async Task SyncFrom_WithoutStarBlogPublish_UsesLocalMarkdown() {
        var path = await Article("draft.md", "# 草稿\n\n![图](pic.jpg)");
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false,
            workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));
        await shell.Workspace.OpenPathAsync(path);

        var viewModel = CreateViewModel();
        viewModel.SyncFrom(shell.PublishPage);

        viewModel.MarkdownSourceMessage.Should().Contain("尚未在 StarBlog 发布");
        viewModel.ContentSourceUrl.Should().BeEmpty();
        viewModel.FormattedHtml.Should().Contain("pic.jpg");
    }

    [Fact]
    public async Task SyncFrom_PreservesLongEnglishMetadataWithinWeightedLimits() {
        var title = new string('A', 128);
        var summary = new string('a', 240);
        var path = await Article("english.md", $"# {title}\n\n正文内容");
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false,
            workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));
        await shell.Workspace.OpenPathAsync(path);
        shell.PublishPage.ArticleDescription = summary;

        var viewModel = CreateViewModel();
        viewModel.SyncFrom(shell.PublishPage);

        viewModel.ArticleTitle.Should().Be(title);
        viewModel.TitleCounterText.Should().Be("64/64");
        viewModel.Digest.Should().Be(summary);
        viewModel.DigestCounterText.Should().Be("120/120");
    }

    private async Task<string> Article(string name, string content) {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static WeChatViewModel CreateViewModel() =>
        new(Mock.Of<IHttpClientFactory>());

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
