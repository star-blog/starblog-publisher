using FluentAssertions;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class ArticleImageCatalogTests {
    [Fact]
    public void FromHtml_ResolvesLocalPathsAndKeepsRemoteQueryStrings() {
        var article = Path.Combine(Path.GetTempPath(), "articles", "post.md");
        var images = ArticleImageCatalog.FromHtml("""
            <img src="./photos/a%20b.png" alt="第一张">
            <img src='https://images.example/photo?id=1&amp;size=2' alt='在线图'>
            <img src="./photos/a%20b.png" alt="重复">
            """, article);

        images.Should().Equal(
            new ArticleImageSource(Path.Combine(Path.GetDirectoryName(article)!, "photos", "a b.png"), "第一张"),
            new ArticleImageSource("https://images.example/photo?id=1&size=2", "在线图"));
    }

    [Fact]
    public void FromHtml_ReadsRenderedMarkdownReferencesNestedImagesAndHtml() {
        var html = new WeChatFormattingService().Format("""
            # 图片文章

            > ![引用图][photo]

            [photo]: https://images.example/reference.png

            - **![列表图](https://images.example/list.png)**

            <img src="https://images.example/html.png" alt="HTML 图">

            ```html
            <img src="https://images.example/code.png">
            ```
            """, "").Html;

        ArticleImageCatalog.FromHtml(html, null).Select(image => image.Source).Should().Equal(
            "https://images.example/reference.png", "https://images.example/list.png", "https://images.example/html.png");
    }

    [Fact]
    public void FromHtml_SkipsCommentsEmptySourcesAndUnsupportedSchemes() {
        ArticleImageCatalog.FromHtml("""
            <!-- <img src="hidden.png"> -->
            <img src="">
            <img src="data:image/png;base64,AAAA">
            <img src="ftp://images.example/a.png">
            <img data-src="ignored.png" alt=" src='also-ignored.png'">
            <IMG SRC=//images.example/visible.png>
            """, null).Should().Equal(new ArticleImageSource("https://images.example/visible.png", "正文图片 1"));
    }

    [Fact]
    public void FromHtml_SupportsFileUrisAndAbsoluteLocalPaths() {
        var path = Path.Combine(Path.GetTempPath(), "cover photo.png");
        ArticleImageCatalog.FromHtml($"<img src='{new Uri(path).AbsoluteUri}'><img src='{path}'>", null)
            .Should().ContainSingle().Which.Source.Should().Be(path);
    }
}
