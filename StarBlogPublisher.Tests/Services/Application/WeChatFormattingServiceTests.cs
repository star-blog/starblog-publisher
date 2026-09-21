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
        result.Html.Should().Contain("value = ");
        result.Html.Should().NotContain("<pre style=\"font-family:Consolas,Menlo,monospace;font-size:0.9em;background:#F1F5F9");
        System.Text.RegularExpressions.Regex.Matches(result.Html, "background:#FF5F56").Count.Should().Be(1);
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
        result.Html.Should().NotContain("<span style=\"color:");
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
