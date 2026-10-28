using FluentAssertions;
using System.Net;
using System.Text.RegularExpressions;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatFormattingServiceTests {
    [Fact]
    public void Format_InsertsChromeAfterStylingAndKeepsFooterLast() {
        var header = """<p style="color:rgb(130, 127, 196)">纯原创无AI污染</p><script>alert(1)</script>""";
        var footer = """<p style="color:rgb(77, 96, 138)" onclick="evil()">欢迎关注</p><mp-common-profile data-nickname="曦远Life" data-signature="生活手记"></mp-common-profile>""";
        var service = new WeChatFormattingService();
        var plain = service.Format("# 标题\n\n正文 [外部](https://example.com/a)", "标题");
        var withChrome = service.Format(
            "# 标题\n\n正文 [外部](https://example.com/a)",
            "标题",
            headerHtml: header,
            footerHtml: footer);

        withChrome.WordCount.Should().Be(plain.WordCount);
        var html = withChrome.Html;
        html.IndexOf("纯原创无AI污染", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("正文", StringComparison.Ordinal));
        html.IndexOf("引用链接", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("欢迎关注", StringComparison.Ordinal));
        html.Should().Contain("<p style=\"color:rgb(130, 127, 196)\">纯原创无AI污染</p>");
        html.Should().Contain("<p style=\"color:rgb(77, 96, 138)\">欢迎关注</p>");
        html.Should().Contain("data-nickname=\"曦远Life\"");
        html.Should().NotContain("alert(1)");
        html.Should().NotContain("onclick");
        html.Should().EndWith("</mp-common-profile></section>");
    }

    [Fact]
    public void Format_KeepsChromeOutsideCardSections() {
        var html = new WeChatFormattingService().Format(
            "# 标题\n\n正文",
            "标题",
            "warm-card",
            convertExternalLinksToReferences: false,
            headerHtml: "<p>文章开头标记</p>",
            footerHtml: "<p>文末引导标记</p>").Html;

        html.IndexOf("文章开头标记", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("max-width:800px", StringComparison.Ordinal));
        html.LastIndexOf("文末引导标记", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.LastIndexOf("max-width:800px", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_GeneratesWeChatInlineHtmlAndPreservesImageSource() {
        var service = new WeChatFormattingService();

        var result = service.Format("# 中文标题\n\n中文Test内容。\n\n![封面](images/cover.png)", "文件标题", "ocean-card");

        result.Title.Should().Be("中文标题");
        result.Theme.Id.Should().Be("ocean-card");
        result.WordCount.Should().BeGreaterThan(0);
        result.Html.Should().Contain("<section style=\"max-width:677px");
        result.Html.Should().Contain("<h1");
        result.Html.Should().Contain("color:#3a4150");
        result.Html.Should().Contain("src=\"images/cover.png\"");
        result.Html.Should().Contain("border-radius:12px");
    }

    [Fact]
    public void Format_UsesFrontMatterTitleAndRemovesFrontMatterFromHtml() {
        var service = new WeChatFormattingService();

        var result = service.Format("---\ntitle: 前言标题\n---\n\n正文内容", "文件标题");

        result.Title.Should().Be("前言标题");
        result.Html.Should().NotContain("title: 前言标题");
    }

    [Fact]
    public void Format_ConvertsExternalLinksToNumberedReferencesInReadingOrder() {
        var markdown = """
            比如《[当人人都能用 AI 写代码时，我为什么选择重回 Django?](https://blog.deali.cn/p/refocus-on-django)》。

            后面看了 [Armin Ronacher](https://lucumr.pocoo.org/about/) 的文章《[90%](https://lucumr.pocoo.org/2025/9/29/90-percent/)》。
            """;

        var html = new WeChatFormattingService().Format(markdown, "标题").Html;
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", string.Empty));

        Regex.Matches(html, @"<sup\b[^>]*>\[\d+\]</sup>").Select(match => match.Value)
            .Should().HaveCount(3);
        text.Should().Contain("当人人都能用 AI 写代码时，我为什么选择重回 Django?[1]");
        text.Should().Contain("Armin Ronacher[2]");
        text.Should().Contain("90%[3]");
        text.Should().Contain("引用链接");
        text.Should().Contain("[1] 当人人都能用 AI 写代码时，我为什么选择重回 Django?：https://blog.deali.cn/p/refocus-on-django");
        text.Should().Contain("[2] Armin Ronacher：https://lucumr.pocoo.org/about/");
        text.Should().Contain("[3] 90%：https://lucumr.pocoo.org/2025/9/29/90-percent/");
        html.Should().NotContain("href=\"https://");
    }

    [Fact]
    public void Format_ReusesNumbersForRepeatedUrlsAndStartsFreshForEachArticle() {
        var service = new WeChatFormattingService();
        var html = service.Format("[第一次](https://example.com/a) [第二个](https://example.com/b) [再次](https://example.com/a)", "标题").Html;

        Regex.Matches(html, @"<sup\b[^>]*>(?<number>\[\d+\])</sup>")
            .Select(match => match.Groups["number"].Value).Should().Equal("[1]", "[2]", "[1]");
        Regex.Matches(html, "https://example.com/a").Count.Should().Be(1);
        html.Should().NotContain("[3]");

        var next = service.Format("[下一篇](https://example.com/c)", "标题").Html;
        next.Should().Contain(">[1]</sup>");
        next.Should().NotContain("example.com/a");
    }

    [Theory]
    [InlineData("[引用式][source]\n\n[source]: https://example.com/path_(part) \"标题\"")]
    [InlineData("<https://example.com/path_(part)>")]
    [InlineData("https://example.com/path_(part)")]
    [InlineData("<a title='a > b' href='https://example.com/path_(part)'>HTML</a>")]
    [InlineData("<a href=https://example.com/path_(part)>HTML</a>")]
    public void Format_ConvertsSupportedLinkSyntaxWithoutTruncatingUrls(string markdown) {
        var html = new WeChatFormattingService().Format(markdown, "标题").Html;

        html.Should().Contain(">[1]</sup>");
        html.Should().Contain("引用链接");
        html.Should().Contain("https://example.com/path_(part)");
        html.Should().NotContain("href=");
    }

    [Fact]
    public void Format_PreservesFormattingAndSafelyEncodesReferenceText() {
        var html = new WeChatFormattingService().Format(
            "[**粗体**与 `code` &lt;img&gt; &amp; 更多](https://example.com/search?a=1&b=2)", "标题").Html;

        html.Should().Contain("<strong");
        html.Should().Contain("<code");
        html.Should().Contain("&lt;img&gt;");
        html.Should().NotContain("<img>");
        html.Should().Contain("https://example.com/search?a=1&amp;b=2");
        html.Should().NotContain("&amp;amp;");
        WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", string.Empty))
            .Should().Contain("[1] 粗体与 code <img> & 更多：https://example.com/search?a=1&b=2");
    }

    [Fact]
    public void Format_OnlyExemptsTheExactWeChatArticleHost() {
        var html = new WeChatFormattingService().Format("""
            [微信文章](https://MP.WEIXIN.QQ.COM/s/article)
            [微信相对协议](//mp.weixin.qq.com/s/other)
            [其他站点](https://mp.weixin.qq.com.example.com/article)
            [外部相对协议](//example.com/article)
            """, "标题").Html;

        html.Should().Contain("href=\"https://MP.WEIXIN.QQ.COM/s/article\"");
        html.Should().Contain("href=\"//mp.weixin.qq.com/s/other\"");
        html.Should().NotContain("href=\"https://mp.weixin.qq.com.example.com");
        html.Should().NotContain("href=\"//example.com");
        html.Should().Contain(">[1]</sup>");
        html.Should().Contain(">[2]</sup>");
        html.Should().NotContain(">[3]</sup>");
    }

    [Fact]
    public void Format_DoesNotReferenceImagesCodeCommentsOrNonWebLinks() {
        var html = new WeChatFormattingService().Format("""
            [章节](#section) [相对地址](../post.md) [邮件](mailto:author@example.com)

            ![图片](https://example.com/image.png)

            `[行内代码](https://example.com/inline)`

            ```html
            <a href="https://example.com/code">代码</a>
            ```

            <pre><a href="https://example.com/raw-code">原始代码</a></pre>

            <!-- <a href="https://example.com/comment">注释</a> -->
            """, "标题").Html;

        html.Should().Contain("href=\"#section\"");
        html.Should().Contain("href=\"../post.md\"");
        html.Should().Contain("href=\"mailto:author@example.com\"");
        html.Should().Contain("src=\"https://example.com/image.png\"");
        html.Should().Contain("[行内代码](https://example.com/inline)");
        html.Should().NotContain("引用链接");
        html.Should().NotContain("<sup");
    }

    [Fact]
    public void Format_PreservesLinkedImagesAndUsesAltTextForTheirReferences() {
        var html = new WeChatFormattingService().Format(
            "[![示意图](images/diagram.png)](https://example.com/article)", "标题").Html;

        html.Should().Contain("src=\"images/diagram.png\"");
        html.Should().Contain(">[1]</sup>");
        WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]*>", string.Empty))
            .Should().Contain("[1] 示意图：https://example.com/article");
    }

    [Theory]
    [InlineData("newspaper")]
    [InlineData("warm-card")]
    public void Format_UsesThemeReferenceStylesAndRetainsVisibleReferencesForDrafts(string themeId) {
        var result = new WeChatFormattingService().Format("[链接](https://example.com/article)", "标题", themeId);
        var uploaded = WeChatDraftPublishApplicationService.MinifyHtmlForWeChatDraft(result.Html);

        uploaded.Should().Contain(result.Theme.Styles["footnote_section"]);
        uploaded.Should().Contain(result.Theme.Styles["footnote_title"]);
        uploaded.Should().Contain(result.Theme.Styles["footnote_item"]);
        uploaded.Should().Contain(result.Theme.Styles["footnote_sup"]);
        uploaded.Should().Contain(">[1]</sup>");
        uploaded.Should().Contain("引用链接");
        uploaded.Should().Contain("https://example.com/article");
        uploaded.Should().NotContain("href=");
    }

    [Fact]
    public void Format_CanDisableExternalLinkReferences() {
        var html = new WeChatFormattingService().Format("[链接](https://example.com/article)", "标题",
            convertExternalLinksToReferences: false).Html;

        html.Should().Contain("href=\"https://example.com/article\"");
        html.Should().NotContain("引用链接");
        html.Should().NotContain("<sup");
    }

    [Fact]
    public void Format_KeepsCodeHighlightingAndReferencesIndependentAcrossConcurrentArticles() {
        var service = new WeChatFormattingService();

        Parallel.For(0, 32, index => {
            var html = service.Format($"[文章](https://example.com/{index})\n\n```csharp\nvar value = \"article-{index}\";\n```", "标题").Html;

            html.Should().Contain($"article-{index}");
            html.Should().Contain($"https://example.com/{index}</span>");
            html.Should().Contain(">[1]</sup>");
            html.Should().NotContain(">[2]</sup>");
        });
    }

    [Fact]
    public void Format_HighlightsCSharpFencedCodeWithInlineStyles() {
        var service = new WeChatFormattingService();

        var result = service.Format("```csharp\npublic string Name => \"value\";\n```", "文件标题");

        result.Html.Should().Contain("<span style=\"");
        result.Html.Should().Contain("public");
        result.Html.Should().Contain("background:#f0ede8");
        result.Html.Should().NotContain("<script");
        result.Html.Should().NotContain("<link");
        result.Html.Should().NotContain("<style");
    }

    [Fact]
    public void Format_StylesInlineCodeWithoutApplyingThatStyleToFencedCode() {
        var service = new WeChatFormattingService();

        var result = service.Format("行内 `value`。\n\n```csharp\nvar value = 1;\n```", "文件标题");

        result.Html.Should().Contain("<code style=\"");
        result.Html.Should().Contain("font-family:'SF Mono', Consolas, monospace");
        result.Html.Should().Contain("value&nbsp;=&nbsp;");
        result.Html.Should().NotContain("<pre style=\"font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9");
        System.Text.RegularExpressions.Regex.Matches(result.Html, "color:#FF5F56").Count.Should().Be(1);
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
        var pre = System.Text.RegularExpressions.Regex.Match(result.Html, @"<pre\b[\s\S]*?</pre>").Value;
        pre.Should().NotContain("<span style=\"color:");
    }

    [Theory]
    [InlineData("text")]
    [InlineData("csharp")]
    public void Format_EncodesCodeLinesAndAlignmentForWeChatEditor(string language) {
        var service = new WeChatFormattingService();
        var markdown = $"```{language}\nUsage:\n  --help    Show help\n\n  --version Show version\n```";

        var html = service.Format(markdown, "Code sample").Html;
        var pre = System.Text.RegularExpressions.Regex.Match(html, @"<pre\b[\s\S]*?</pre>").Value;

        pre.Should().Contain("white-space:normal");
        pre.Should().Contain("<br/>");
        pre.Should().Contain("&nbsp;&nbsp;--help");
        pre.Should().Contain("&nbsp;&nbsp;&nbsp;&nbsp;Show");
        pre.Should().Contain("<br/><br/>");
        pre.Should().NotContain("--help    Show");

        var uploaded = WeChatDraftPublishApplicationService.MinifyHtmlForWeChatDraft(html);
        uploaded.Should().Contain("&nbsp;&nbsp;--help");
        uploaded.Should().Contain("<br/><br/>");
    }

    [Fact]
    public void Format_UsesVisibleWindowControlsThatSurviveDraftHtmlMinification() {
        var service = new WeChatFormattingService();
        var html = service.Format("```text\ncommand\n```", "Code sample").Html;
        var uploaded = WeChatDraftPublishApplicationService.MinifyHtmlForWeChatDraft(html);

        uploaded.Should().Contain("color:#FF5F56;font-size:18px;line-height:1;margin-right:6px\">●</span>");
        uploaded.Should().Contain("color:#FFBD2E;font-size:18px;line-height:1;margin-right:6px\">●</span>");
        uploaded.Should().Contain("color:#27C93F;font-size:18px;line-height:1;margin-right:6px\">●</span>");
        uploaded.Should().NotContain("></span>");
    }

    [Fact]
    public void Format_ResolvesLegacyTechThemeToGitHub() {
        var service = new WeChatFormattingService();

        var result = service.Format("# 标题\n\n**强调**", "文件标题", "tech");

        result.Theme.Id.Should().Be("github");
        result.Html.Should().Contain("#0550ae");
    }

    [Fact]
    public void Format_ConvertsListsAndSplitsCardThemes() {
        var service = new WeChatFormattingService();

        var result = service.Format("# 标题\n\n- 条目一\n- 条目二\n\n## 第二节\n\n段落。", "文件标题", "warm-card");

        result.Theme.Id.Should().Be("warm-card");
        result.Html.Should().Contain("•");
        result.Html.Should().Contain("条目一");
        result.Html.Should().NotContain("<ul");
        System.Text.RegularExpressions.Regex.Matches(result.Html, "background-image:linear-gradient").Count.Should().BeGreaterThan(1);
    }
}

public class WeChatThemeCatalogTests {
    [Fact]
    public void All_LoadsWechatPubThemesInDisplayOrder() {
        WeChatThemeCatalog.All.Should().HaveCount(33);
        WeChatThemeCatalog.All[0].Id.Should().Be("newspaper");
        WeChatThemeCatalog.All[1].Id.Should().Be("warm-card");
        WeChatThemeCatalog.All.Select(theme => theme.Id).Should().OnlyHaveUniqueItems();
        WeChatThemeCatalog.All.Should().OnlyContain(theme => theme.Styles.Count > 0);
        WeChatThemeCatalog.All.Should().Contain(theme => theme.Id == "github" && theme.Category == "科技产品");
        WeChatThemeCatalog.All.Should().Contain(theme => theme.Id == "warm-card" && theme.Card != null);
    }

    [Theory]
    [InlineData(null, "newspaper")]
    [InlineData("tech", "github")]
    [InlineData("missing", "newspaper")]
    [InlineData("BAUHAUS", "bauhaus")]
    public void NormalizeId_MapsAliasesAndUnknownValues(string? input, string expected) {
        WeChatThemeCatalog.NormalizeId(input).Should().Be(expected);
    }
}
