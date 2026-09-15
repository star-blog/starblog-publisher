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
}
