using FluentAssertions;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class AboutViewModelTests {
    [Fact]
    public void Constructor_ExposesProductIdentityAndSections() {
        var viewModel = new AboutViewModel();

        viewModel.Title.Should().Be("关于");
        viewModel.AppName.Should().Be("StarBlog Publisher");
        viewModel.Tagline.Should().Contain("StarBlog");
        viewModel.AppVersion.Should().NotBeNullOrWhiteSpace();
        viewModel.VersionLabel.Should().StartWith("版本 ");
        viewModel.License.Should().Be("Apache-2.0");
        viewModel.Features.Should().HaveCount(6);
        viewModel.TechStack.Should().NotBeEmpty();
        viewModel.Links.Should().Contain(link => link.Url.Contains("starblog-publisher"));
        viewModel.HasUpdateStatus.Should().BeFalse();
        viewModel.IsUpdateAvailable.Should().BeFalse();
        viewModel.CheckForUpdateCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("v3.1.0", "3.0.0", true)]
    [InlineData("3.0.1", "3.0.0", true)]
    [InlineData("v3.0.0", "3.0.0", false)]
    [InlineData("v2.9.0", "3.0.0", false)]
    [InlineData("v3.0.1-beta.1", "3.0.0", true)]
    [InlineData("not-a-version", "3.0.0", false)]
    [InlineData("v3.1.0", "开发版本", false)]
    public void IsNewerVersion_ComparesReleaseTags(string latest, string current, bool expected) {
        AboutViewModel.IsNewerVersion(latest, current).Should().Be(expected);
    }
}
