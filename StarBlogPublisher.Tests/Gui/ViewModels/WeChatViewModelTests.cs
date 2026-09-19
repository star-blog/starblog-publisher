using FluentAssertions;
using Moq;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class WeChatViewModelTests {
    [Fact]
    public void Constructor_SelectsUsableDefaults() {
        var viewModel = CreateViewModel();

        viewModel.Themes.Should().NotBeEmpty();
        viewModel.SelectedTheme.Should().NotBeNull();
        viewModel.SelectedCoverSource!.Id.Should().Be("local");
        viewModel.SelectedCoverSize!.Id.Should().Be("headline");
        viewModel.SelectedRandomCoverProvider.Should().NotBeNull();
        viewModel.IsLocalCoverSource.Should().BeTrue();
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

    private static WeChatViewModel CreateViewModel() =>
        new(Mock.Of<IHttpClientFactory>());
}
