using FluentAssertions;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Utils;

public class MarkdownHighlightTokenizerTests {
    [Fact]
    public void Tokenize_Heading_HighlightsWholeLine() {
        var spans = MarkdownHighlightTokenizer.Tokenize("## Hello world", inFencedCode: false);

        spans.Should().ContainSingle()
            .Which.Should().Be(new MarkdownHighlightSpan(0, 14, MarkdownHighlightKind.Heading));
    }

    [Fact]
    public void Tokenize_Quote_HighlightsWholeLine() {
        var spans = MarkdownHighlightTokenizer.Tokenize("> quoted", inFencedCode: false);

        spans.Should().ContainSingle()
            .Which.Kind.Should().Be(MarkdownHighlightKind.Quote);
    }

    [Fact]
    public void Tokenize_InlineCodeAndBoldAndLink() {
        var line = "Use `code` and **bold** plus [docs](https://example.com)";
        var spans = MarkdownHighlightTokenizer.Tokenize(line, inFencedCode: false);

        spans.Select(span => span.Kind).Should().Equal(
            MarkdownHighlightKind.InlineCode,
            MarkdownHighlightKind.Bold,
            MarkdownHighlightKind.Link);
    }

    [Fact]
    public void Tokenize_FencedCode_CoversWholeLine() {
        MarkdownHighlightTokenizer.IsFenceDelimiter("```csharp").Should().BeTrue();

        var inside = MarkdownHighlightTokenizer.Tokenize("var x = 1;", inFencedCode: true);
        inside.Should().ContainSingle()
            .Which.Kind.Should().Be(MarkdownHighlightKind.FencedCode);
    }

    [Fact]
    public void Tokenize_DoesNotTreatHashWithoutSpaceAsHeading() {
        var spans = MarkdownHighlightTokenizer.Tokenize("#not-a-heading", inFencedCode: false);
        spans.Should().BeEmpty();
    }
}
