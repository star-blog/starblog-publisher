using FluentAssertions;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatFormattingServiceTests {
    [Fact]
    public void Format_GeneratesWeChatInlineHtmlAndPreservesImageSource() {
        var service = new WeChatFormattingService();

        var result = service.Format("# 中文标题\n\n中文Test内容。\n\n![封面](images/cover.png)", "文件标题", "ocean-card");

        result.Title.Should().Be("中文标题");
        result.Theme.Id.Should().Be("ocean-card");
        result.WordCount.Should().BeGreaterThan(0);
        result.Html.Should().Contain("<section style=\"max-width:677px");
        result.Html.Should().Contain("<h1");
        result.Html.Should().Contain("color:#176B87");
        result.Html.Should().Contain("src=\"images/cover.png\"");
        result.Html.Should().Contain("border-radius:4px\" />");
    }

    [Fact]
    public void Format_UsesFrontMatterTitleAndRemovesFrontMatterFromHtml() {
        var service = new WeChatFormattingService();

        var result = service.Format("---\ntitle: 前言标题\n---\n\n正文内容", "文件标题");

        result.Title.Should().Be("前言标题");
        result.Html.Should().NotContain("title: 前言标题");
    }

    [Fact]
    public void Format_HighlightsCSharpFencedCodeWithInlineStyles() {
        var service = new WeChatFormattingService();

        var result = service.Format("```csharp\npublic string Name => \"value\";\n```", "文件标题");

        result.Html.Should().Contain("<span style=\"");
        result.Html.Should().Contain("public");
        result.Html.Should().Contain("background:#1E293B");
        result.Html.Should().NotContain("<script");
        result.Html.Should().NotContain("<link");
        result.Html.Should().NotContain("<style");
    }

    [Fact]
    public void Format_StylesInlineCodeWithoutApplyingThatStyleToFencedCode() {
        var service = new WeChatFormattingService();

        var result = service.Format("行内 `value`。\n\n```csharp\nvar value = 1;\n```", "文件标题");

        result.Html.Should().Contain("<code style=\"font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9");
        result.Html.Should().Contain("value = ");
        result.Html.Should().NotContain("<pre style=\"font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9");
        System.Text.RegularExpressions.Regex.Matches(result.Html, "<section style=\"margin:20px").Count.Should().Be(1);
    }

    [Fact]
    public void Format_DoesNotChangeCjkSpacingInsideFencedCode() {
        var service = new WeChatFormattingService();

        var result = service.Format("```csharp\nvar text = \"你好World\";\n```", "文件标题");

        result.Html.Should().Contain("你好World");
        result.Html.Should().NotContain("你好 World");
    }

    [Theory]
    [InlineData("go")]
    [InlineData("bash")]
    [InlineData("yaml")]
    public void Format_FallsBackForUnsupportedCodeLanguage(string language) {
        var service = new WeChatFormattingService();

        var result = service.Format($"```{language}\n你好World\n```", "文件标题");

        result.Html.Should().Contain("<pre");
        result.Html.Should().Contain("你好World");
        result.Html.Should().NotContain("<span style=\"");
    }
}
