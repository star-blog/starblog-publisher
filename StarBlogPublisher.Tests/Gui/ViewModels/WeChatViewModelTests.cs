using System.Reflection;
using FluentAssertions;
using Moq;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class WeChatViewModelTests {
    [Fact]
    public void Constructor_SelectsUsableDefaults() {
        var viewModel = CreateViewModel();

        viewModel.Themes.Should().NotBeEmpty();
        viewModel.SelectedTheme.Should().NotBeNull();
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

        viewModel.ArticleTitle.Should().HaveLength(32);
        viewModel.TitleCounterText.Should().Be("32/32");
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
    public void Digest_IsClampedToWeChatLimit() {
        var viewModel = CreateViewModel();

        viewModel.Digest = new string('摘', 150);

        viewModel.Digest.Should().HaveLength(120);
        viewModel.DigestCounterText.Should().Be("120/120");
        viewModel.DigestRemainingLength.Should().Be(0);
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

    private static WeChatViewModel CreateViewModel() =>
        new(Mock.Of<IHttpClientFactory>());
}
