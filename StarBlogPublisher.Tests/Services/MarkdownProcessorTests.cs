using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Services;

public class MarkdownProcessorTests {
    [Fact]
    public async Task Processing_IgnoresMetadataInBodySummaryAndImages_AndKeepsSource() {
        const string markdown = "---\ntypora-copy-images-to: ${filename}.assets\ncover: '![metadata](metadata.png)'\n---\n\n## Body\n\nVisible text.\n\n![body](body.png)";
        var sourcePath = Path.Combine(Path.GetTempPath(), "article.md");
        var post = new BlogPost { Content = markdown };
        var processor = new MarkdownProcessor(sourcePath, post);

        var processed = await processor.MarkdownParse(enableCompression: false);

        processed.Should().Contain("Visible text.").And.NotContain("typora-copy-images-to").And.NotContain("metadata.png");
        processor.GetSummary(100).Should().StartWith("Body").And.NotContain("metadata");
        processor.ExtractImagePaths().Should().Equal(Path.Combine(Path.GetTempPath(), "body.png"));
        post.Content.Should().Be(markdown);
    }
}
