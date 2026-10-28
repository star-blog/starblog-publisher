using System.Text;
using FluentAssertions;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatChromeHtmlTests {
    [Fact]
    public void DecodeClipboardBytes_ReadsUtf8HtmlFormatInsteadOfAnsi() {
        var clipboard = "Version:0.9\r\n<!--StartFragment--><p>欢迎点击上方蓝字</p><!--EndFragment-->\0";
        var text = WeChatChromeHtml.DecodeClipboardBytes(Encoding.UTF8.GetBytes(clipboard));

        text.Should().Contain("欢迎点击上方蓝字");
        WeChatChromeHtml.TryExtractFragment(text).Should().Be("<p>欢迎点击上方蓝字</p>");
    }

    [Fact]
    public void TryExtractFragment_ReturnsNullWhenTheClipboardHasNoFragment() {
        WeChatChromeHtml.TryExtractFragment("只有纯文本").Should().BeNull();
        WeChatChromeHtml.TryExtractFragment("<!--StartFragment-->   <!--EndFragment-->").Should().BeNull();
    }

    [Fact]
    public void Sanitize_DropsScriptsAndEventHandlersAndKeepsInlineStyles() {
        var fragment = """
            <p style="text-align: center; color: rgb(79, 79, 79);">🌟欢迎点击上方蓝字<strong style="color: rgb(77, 96, 138);">“曦远Life”</strong></p>
            <script>alert(1)</script>
            <p onclick="evil()" href="javascript:steal()">纯原创无AI污染</p>
            """;

        var sanitized = WeChatChromeHtml.Sanitize(fragment);

        sanitized.Should().Contain("color: rgb(79, 79, 79)");
        sanitized.Should().Contain("🌟欢迎点击上方蓝字");
        sanitized.Should().Contain("“曦远Life”");
        sanitized.Should().Contain("纯原创无AI污染");
        sanitized.Should().NotContain("alert(1)");
        sanitized.Should().NotContain("onclick");
        sanitized.Should().NotContain("javascript:");
        sanitized.Should().Contain("href=\"#\"");
    }

    [Fact]
    public void Summarize_DescribesTextSizeAndProfileCards() {
        var html = """<p>纯原创无AI污染，请放心阅读！</p><mp-common-profile data-nickname="曦远Life"></mp-common-profile><mpprofile data-nickname="曦远Code"></mpprofile>""";

        var summary = WeChatChromeHtml.Summarize(html);

        summary.Should().Contain("纯原创无AI污染");
        summary.Should().Contain("2 张公众号名片");
        summary.Should().Contain("字节");
        WeChatChromeHtml.Summarize("  ").Should().Be("未设置");
    }

    [Fact]
    public void ToPreviewHtml_ReplacesProfileCardsWithoutChangingTheStoredFragment() {
        var source = """<p>正文</p><mp-common-profile data-nickname="曦远Life" data-signature="一位独立开发者的生活美学手记"></mp-common-profile>""";

        var preview = WeChatChromeHtml.ToPreviewHtml(source);

        preview.Should().Contain("公众号名片 · 曦远Life");
        preview.Should().Contain("一位独立开发者的生活美学手记");
        preview.Should().NotContain("mp-common-profile");
        preview.Should().Contain("正文");
        source.Should().Contain("mp-common-profile");
    }

    [Fact]
    public void ContainsIndexInProfileCard_SkipsImagesInsideTheCardOnly() {
        var html = """<p><img src="a.png"></p><mp-common-profile data-headimg="http://mmbiz.qpic.cn/a"><img src="avatar.png"></mp-common-profile><img src="b.png">""";

        WeChatChromeHtml.ContainsIndexInProfileCard(html, html.IndexOf("avatar.png", StringComparison.Ordinal)).Should().BeTrue();
        WeChatChromeHtml.ContainsIndexInProfileCard(html, html.IndexOf("a.png", StringComparison.Ordinal)).Should().BeFalse();
        WeChatChromeHtml.ContainsIndexInProfileCard(html, html.IndexOf("b.png", StringComparison.Ordinal)).Should().BeFalse();
    }
}
