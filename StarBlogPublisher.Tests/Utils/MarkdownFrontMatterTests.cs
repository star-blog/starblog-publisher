using FluentAssertions;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Utils;

public class MarkdownFrontMatterTests {
    [Theory]
    [InlineData("---\ntypora-copy-images-to: ${filename}.assets\n---\n\n## 前言", "\n## 前言")]
    [InlineData("\uFEFF--- \r\ntitle: Test\r\n---\t\r\n# Body", "# Body")]
    [InlineData("---\ntitle: Test\n...\nBody", "Body")]
    [InlineData("---\ntitle: Test\n---", "")]
    [InlineData("---\n---\nBody", "Body")]
    public void Remove_ClosedHeader_ReturnsOnlyBody(string markdown, string body) {
        MarkdownFrontMatter.Remove(markdown).Should().Be(body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# Title\n\n---\ntitle: Body text\n---\nEnd")]
    [InlineData("```yaml\n---\ntitle: Example\n---\n```")]
    [InlineData("---\ntitle: Unclosed\n\n# Body")]
    [InlineData("---\ntitle: Unclosed\n--- more text\nBody")]
    [InlineData("---\n\n# Body")]
    public void Remove_WithoutClosedHeader_PreservesMarkdown(string markdown) {
        MarkdownFrontMatter.Remove(markdown).Should().Be(markdown);
    }

    [Fact]
    public void Remove_PreserveSourcePositions_KeepsBodyOffsetsAndNewlines() {
        const string header = "\uFEFF---\r\ntitle: Metadata\r\n---\r\n";
        const string body = "\r\n## Body\r\n\r\nText";
        var markdown = header + body;

        var filtered = MarkdownFrontMatter.Remove(markdown, preserveSourcePositions: true);

        filtered.Length.Should().Be(markdown.Length);
        filtered[header.Length..].Should().Be(body);
        filtered[..header.Length].Should().NotContain("title");
        for (var index = 0; index < header.Length; index++) {
            filtered[index].Should().Be(markdown[index] is '\r' or '\n' ? markdown[index] : ' ');
        }
    }
}
