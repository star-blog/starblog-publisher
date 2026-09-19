using FluentAssertions;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class ImageGalleryViewModelTests {
    [Fact]
    public void LoadImages_ReplacesTheGalleryAndCalculatesTheImageCount() {
        var firstPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");
        var secondPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.jpg");
        var viewModel = new ImageGalleryViewModel();

        viewModel.LoadImages([firstPath, secondPath]);

        viewModel.ImageCount.Should().Be(2);
        viewModel.Images.Should().HaveCount(2);
        viewModel.Images.Select(image => image.FilePath).Should().ContainInOrder(firstPath, secondPath);
        viewModel.Images.Should().OnlyContain(image => !image.Exists && image.ImageBitmap == null);
    }

    [Fact]
    public void RefreshCommand_RecreatesTheExistingImageMetadata() {
        var imagePath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.png");
        var viewModel = new ImageGalleryViewModel();
        viewModel.LoadImages([imagePath]);
        var original = viewModel.Images[0];

        viewModel.RefreshCommand.Execute(null);

        viewModel.ImageCount.Should().Be(1);
        viewModel.Images.Should().ContainSingle();
        viewModel.Images[0].Should().NotBeSameAs(original);
        viewModel.Images[0].FilePath.Should().Be(imagePath);
        viewModel.Images[0].Exists.Should().BeFalse();
    }
}
