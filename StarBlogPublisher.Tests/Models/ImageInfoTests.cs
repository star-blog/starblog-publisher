using FluentAssertions;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Tests.Models;

public class ImageInfoTests {
    [Fact]
    public void Create_FileExists_SetsPropertiesCorrectly() {
        var tempFile = Path.GetTempFileName();
        File.WriteAllBytes(tempFile, new byte[1024]);

        try {
            var info = ImageInfo.Create(tempFile);

            info.Should().NotBeNull();
            info.Exists.Should().BeTrue();
            info.FilePath.Should().Be(tempFile);
            info.FileName.Should().Be(Path.GetFileName(tempFile));
            info.FileSize.Should().Contain("KB"); // 1024 bytes = 1 KB
        }
        finally {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Create_FileDoesNotExist_SetsExistsFalse() {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent_file_12345.png");

        var info = ImageInfo.Create(nonExistentPath);

        info.Exists.Should().BeFalse();
        info.FilePath.Should().Be(nonExistentPath);
        info.FileName.Should().Be("nonexistent_file_12345.png");
    }

    [Fact]
    public void Create_SetsFileNameFromPath() {
        var tempFile = Path.GetTempFileName();

        try {
            var info = ImageInfo.Create(tempFile);
            info.FileName.Should().Be(Path.GetFileName(tempFile));
        }
        finally {
            File.Delete(tempFile);
        }
    }
}
