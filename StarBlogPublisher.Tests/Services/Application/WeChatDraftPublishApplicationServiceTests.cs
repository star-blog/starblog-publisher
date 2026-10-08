using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class WeChatDraftPublishApplicationServiceTests {
    [Fact]
    public async Task CreateWeChatMediaContent_UsesQuotedDispositionWithoutFilenameStar() {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        using var content = WeChatDraftPublishApplicationService.CreateWeChatMediaContent(jpegBytes, ".jpg");

        var contentType = content.Headers.ContentType!.ToString();
        contentType.Should().StartWith("multipart/form-data; boundary=");
        contentType.Should().NotContain("boundary=\"");

        var bodyText = await content.ReadAsStringAsync();
        bodyText.Should().Contain("Content-Disposition: form-data; name=\"media\"; filename=\"media.jpg\"");
        bodyText.Should().Contain("Content-Type: image/jpeg");
        bodyText.Should().NotContain("filename*=");

        var bodyBytes = await content.ReadAsByteArrayAsync();
        bodyBytes.Should().ContainInOrder(jpegBytes);
    }

    [Fact]
    public async Task CreateWeChatMediaContent_NormalizesJpegExtensionAndPngMime() {
        using var jpeg = WeChatDraftPublishApplicationService.CreateWeChatMediaContent([1, 2, 3], ".JPEG");
        (await jpeg.ReadAsStringAsync()).Should().Contain("filename=\"media.jpg\"");

        using var png = WeChatDraftPublishApplicationService.CreateWeChatMediaContent([1, 2, 3], ".png");
        var pngBody = await png.ReadAsStringAsync();
        pngBody.Should().Contain("filename=\"media.png\"");
        pngBody.Should().Contain("Content-Type: image/png");
    }

    [Fact]
    public void TruncateTitle_CapsAt32Characters() {
        var longTitle = new string('题', 80);

        var truncated = WeChatDraftPublishApplicationService.TruncateTitle(longTitle);

        truncated.Should().HaveLength(WeChatDraftPublishApplicationService.MaxTitleLength);
        truncated.Should().Be(new string('题', 32));
    }

    [Fact]
    public void TruncateAuthor_CapsAt16Characters() {
        var truncated = WeChatDraftPublishApplicationService.TruncateAuthor(new string('作', 20));

        truncated.Should().HaveLength(WeChatDraftPublishApplicationService.MaxAuthorLength);
        truncated.Should().Be(new string('作', 16));
    }

    [Fact]
    public void PublishedContentSourceUrl_UsesTheStarBlogPostUrlAfterSuccess() {
        var published = PublishResult.Ok(new BlogPost { Title = "标题" }, "https://blog.example/p/post");
        var failed = PublishResult.Fail("发布失败");

        WeChatDraftPublishApplicationService.PublishedContentSourceUrl(published).Should().Be("https://blog.example/p/post");
        WeChatDraftPublishApplicationService.PublishedContentSourceUrl(failed).Should().BeNull();
        WeChatDraftPublishApplicationService.PublishedContentSourceUrl(null).Should().BeNull();
    }

    [Fact]
    public void TryNormalizeContentSourceUrl_RejectsNonHttpLinks() {
        var ok = WeChatDraftPublishApplicationService.TryNormalizeContentSourceUrl(" https://blog.example/p/post ", out var normalized, out var error);

        ok.Should().BeTrue();
        normalized.Should().Be("https://blog.example/p/post");
        error.Should().BeNull();

        WeChatDraftPublishApplicationService.TryNormalizeContentSourceUrl("ftp://blog.example/p/post", out _, out var rejected)
            .Should().BeFalse();
        rejected.Should().Contain("HTTP");
    }

    [Fact]
    public void TruncateDigest_CapsAt120Characters() {
        var longDigest = new string('摘', 150);

        var truncated = WeChatDraftPublishApplicationService.TruncateDigest(longDigest);

        truncated.Should().HaveLength(WeChatDraftPublishApplicationService.MaxDigestLength);
        truncated.Should().Be(new string('摘', 120));
    }

    [Fact]
    public void SerializeWeChatJson_KeepsRawChineseInsteadOfUnicodeEscapes() {
        var json = WeChatDraftPublishApplicationService.SerializeWeChatJson(new {
            title = "团队 Web 开发规范",
            author = "作者",
            digest = "摘要内容"
        });

        json.Should().Contain("团队 Web 开发规范");
        json.Should().Contain("作者");
        json.Should().Contain("摘要内容");
        json.Should().NotContain("\\u56E2");
        json.Should().NotContain("\\u4F5C");
    }

    [Fact]
    public void SerializeWeChatJson_KeepsEmojiInsteadOfSurrogateEscapes() {
        const string emoji = "\U0001F602";
        var json = WeChatDraftPublishApplicationService.SerializeWeChatJson(new {
            content = $"花了一个多小时才到{emoji}",
            title = $"打卡{emoji}"
        });

        json.Should().Contain($"花了一个多小时才到{emoji}");
        json.Should().Contain($"打卡{emoji}");
        json.Should().NotContain("\\uD83D");
        json.Should().NotContain("\\uDE02");
        json.Should().NotContain("\\u");
    }

    [Fact]
    public void SerializeWeChatJson_KeepsLiteralUnicodeEscapeTextAndJsonMetacharacters() {
        // \u005c is backslash; avoids C# treating \uD83D in the source as a surrogate character.
        var literalEscape = "\u005cuD83D\u005cuDE02";
        var json = WeChatDraftPublishApplicationService.SerializeWeChatJson(new {
            content = $"code: {literalEscape} and \"quotes\" and \nnewline"
        });

        json.Should().Contain("\u005c\u005cuD83D\u005c\u005cuDE02");
        json.Should().Contain("\\\"quotes\\\"");
        json.Should().Contain("\\nnewline");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        parsed!["content"].Should().Be($"code: {literalEscape} and \"quotes\" and \nnewline");
    }

    [Fact]
    public void MinifyHtmlForWeChatDraft_CollapsesWhitespaceBetweenTagsButKeepsPreContent() {
        var html = "<ul style=\"margin:0\">\n<li>one</li>\n<li>two</li>\n</ul>\n<pre style=\"x\">line1\nline2\n</pre>";

        var minified = WeChatDraftPublishApplicationService.MinifyHtmlForWeChatDraft(html);

        minified.Should().Contain("<ul style=\"margin:0\"><li>one</li><li>two</li></ul>");
        minified.Should().NotMatchRegex(@">\s*\n\s*<");
        var pre = System.Text.RegularExpressions.Regex.Match(minified, @"<pre[\s\S]*?</pre>").Value;
        pre.Should().Contain("line1\nline2\n");
    }

    [Fact]
    public async Task PublishAsync_UploadsCoverAsPermanentImageMaterial_WithCompatibleMultipart() {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try {
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", """{"access_token":"tok","expires_in":7200}"""),
                (HttpMethod.Post, "cgi-bin/material/add_material", """{"media_id":"cover-media-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/add", """{"media_id":"draft-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/get", """{"news_item":[{"title":"Hello"}]}""")
            ]);
            var service = CreateService(handler, out var settings);
            // Unique credentials avoid cross-test hits on the static access-token cache.
            settings.WeChatAppId = $"app-cover-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";
            settings.WeChatAuthor = "星博作者";

            var longDigest = new string('摘', 150);
            var result = await service.PublishAsync(
                FormatResult("<ul>\n<li>one</li>\n<li>two</li>\n</ul>", "团队 Web 开发规范"),
                Path.GetTempPath(),
                longDigest,
                coverPath);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.DraftMediaId.Should().Be("draft-1");
            result.CoverMediaId.Should().Be("cover-media-1");

            var upload = handler.Requests.Should().ContainSingle(r =>
                r.Method == HttpMethod.Post &&
                r.Uri.AbsoluteUri.Contains("cgi-bin/material/add_material", StringComparison.Ordinal)).Subject;
            upload.Uri.Query.Should().Contain("type=image");
            upload.Uri.Query.Should().NotContain("type=thumb");
            upload.ContentType.Should().StartWith("multipart/form-data; boundary=");
            upload.ContentType.Should().NotContain("boundary=\"");
            upload.BodyText.Should().Contain("name=\"media\"");
            upload.BodyText.Should().Contain("filename=\"media.jpg\"");
            upload.BodyText.Should().NotContain("filename*=");

            var draft = handler.Requests.Should().ContainSingle(r =>
                r.Uri.AbsoluteUri.Contains("cgi-bin/draft/add", StringComparison.Ordinal)).Subject;
            draft.BodyText.Should().Contain("团队 Web 开发规范");
            draft.BodyText.Should().Contain("星博作者");
            draft.BodyText.Should().Contain($"\"digest\":\"{new string('摘', 120)}\"");
            draft.BodyText.Should().NotContain("\\u56E2");
            draft.BodyText.Should().NotContain(new string('摘', 121));
            draft.BodyText.Should().Contain("<ul><li>one</li><li>two</li></ul>");
            draft.BodyText.Should().NotContain("<ul>\\n<li>");
            draft.BodyText.Should().Contain("\"need_open_comment\":1");
            draft.BodyText.Should().Contain("\"only_fans_can_comment\":0");
            draft.BodyText.Should().NotContain("show_cover_pic");
            draft.BodyText.Should().NotContain("content_source_url");
            draft.BodyText.Should().NotContain("cover_info");
            // Returned HTML for preview/copy keeps original formatting with newlines.
            result.FormattedHtml.Should().Contain("<ul>\n<li>one</li>");
        }
        finally {
            File.Delete(coverPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_UploadsContentImagesViaUploadimg(bool webp) {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        var imageDir = Path.Combine(Path.GetTempPath(), $"starblog-imgs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(imageDir);
        var fileName = webp ? "pic.webp" : "pic.png";
        var contentImagePath = Path.Combine(imageDir, fileName);
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        using (var image = new Image<Rgba32>(30, 20, new Rgba32(40, 80, 120))) {
            if (webp) await image.SaveAsWebpAsync(contentImagePath);
            else await image.SaveAsPngAsync(contentImagePath);
        }
        try {
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", """{"access_token":"tok","expires_in":7200}"""),
                (HttpMethod.Post, "cgi-bin/media/uploadimg", """{"url":"https://mmbiz.qpic.cn/uploaded.png"}"""),
                (HttpMethod.Post, "cgi-bin/material/add_material", """{"media_id":"cover-media-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/add", """{"media_id":"draft-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/get", """{"news_item":[{"title":"Hello"}]}""")
            ]);
            var service = CreateService(handler, out var settings);
            settings.WeChatAppId = $"app-content-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";

            var result = await service.PublishAsync(
                FormatResult($"<p><img src=\"{fileName}\" alt=\"x\"></p>"),
                imageDir,
                "summary",
                coverPath);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.ContentImageUrls.Should().Equal("https://mmbiz.qpic.cn/uploaded.png");
            result.FormattedHtml.Should().Contain("https://mmbiz.qpic.cn/uploaded.png");

            handler.Requests.Should().Contain(r =>
                r.Method == HttpMethod.Post &&
                r.Uri.AbsoluteUri.Contains("cgi-bin/media/uploadimg", StringComparison.Ordinal));
            var contentUpload = handler.Requests.Single(r =>
                r.Uri.AbsoluteUri.Contains("cgi-bin/media/uploadimg", StringComparison.Ordinal));
            contentUpload.BodyText.Should().Contain(webp ? "filename=\"media.jpg\"" : "filename=\"media.png\"");
            contentUpload.BodyText.Should().Contain(webp ? "Content-Type: image/jpeg" : "Content-Type: image/png");
        }
        finally {
            File.Delete(coverPath);
            Directory.Delete(imageDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_PreparesRemoteImagesRegardlessOfMimeAndOriginalSize(bool oversized) {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try {
            using var image = new Image<Rgba32>(oversized ? 900 : 30, oversized ? 900 : 20);
            var random = new Random(42);
            image.ProcessPixelRows(accessor => {
                for (var y = 0; y < accessor.Height; y++) {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++) {
                        row[x] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
                    }
                }
            });
            using var source = new MemoryStream();
            if (oversized) await image.SaveAsPngAsync(source);
            else await image.SaveAsWebpAsync(source);
            if (oversized) source.Length.Should().BeGreaterThan(1024 * 1024);
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", """{"access_token":"tok","expires_in":7200}"""),
                (HttpMethod.Get, "images.example", ""),
                (HttpMethod.Post, "cgi-bin/media/uploadimg", """{"url":"https://mmbiz.qpic.cn/prepared.jpg"}"""),
                (HttpMethod.Post, "cgi-bin/material/add_material", """{"media_id":"cover"}"""),
                (HttpMethod.Post, "cgi-bin/draft/add", """{"media_id":"draft"}"""),
                (HttpMethod.Post, "cgi-bin/draft/get", """{"news_item":[{"title":"Hello"}]}""")
            ]) { RemoteImageBytes = source.ToArray() };
            var service = CreateService(handler, out var settings);
            settings.WeChatAppId = $"app-remote-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";

            var result = await service.PublishAsync(
                FormatResult("<img src=\"https://images.example/download?id=1\">"), Path.GetTempPath(), "summary", coverPath);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.FormattedHtml.Should().Contain("https://mmbiz.qpic.cn/prepared.jpg");
            var upload = handler.Requests.Single(r => r.Uri.AbsoluteUri.Contains("cgi-bin/media/uploadimg"));
            upload.BodyText.Should().Contain("filename=\"media.jpg\"");
            var headerEnd = Encoding.UTF8.GetString(upload.BodyBytes).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
            var boundary = upload.ContentType.Split("boundary=")[1];
            var footerSize = Encoding.UTF8.GetByteCount($"\r\n--{boundary}--\r\n");
            var imageBytes = upload.BodyBytes[headerEnd..^footerSize];
            imageBytes.Length.Should().BeLessThanOrEqualTo(1024 * 1024);
            Image.DetectFormat(imageBytes).Name.Should().Be("JPEG");
            using var decoded = Image.Load(imageBytes);
            decoded.Width.Should().BeGreaterThan(0);
        }
        finally { File.Delete(coverPath); }
    }

    [Fact]
    public async Task PublishAsync_KeepsEmojiInDraftContent() {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try {
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", """{"access_token":"tok","expires_in":7200}"""),
                (HttpMethod.Post, "cgi-bin/material/add_material", """{"media_id":"cover-media-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/add", """{"media_id":"draft-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/get", """{"news_item":[{"title":"Hello"}]}""")
            ]);
            var service = CreateService(handler, out var settings);
            settings.WeChatAppId = $"app-emoji-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";

            const string emoji = "\U0001F602";
            var result = await service.PublishAsync(
                FormatResult($"<p>花了一个多小时才到{emoji}</p>", $"打卡{emoji}"),
                Path.GetTempPath(),
                $"摘要{emoji}",
                coverPath);

            result.Success.Should().BeTrue(result.ErrorMessage);
            var draft = handler.Requests.Should().ContainSingle(r =>
                r.Uri.AbsoluteUri.Contains("cgi-bin/draft/add", StringComparison.Ordinal)).Subject;
            draft.BodyText.Should().Contain($"花了一个多小时才到{emoji}");
            draft.BodyText.Should().Contain($"打卡{emoji}");
            draft.BodyText.Should().Contain($"摘要{emoji}");
            draft.BodyText.Should().NotContain("\\uD83D");
            draft.BodyText.Should().NotContain("\\uDE02");
        }
        finally {
            File.Delete(coverPath);
        }
    }

    [Fact]
    public async Task PublishAsync_SendsDraftMetadataAndCenterCrops() {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try {
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", """{"access_token":"tok","expires_in":7200}"""),
                (HttpMethod.Post, "cgi-bin/material/add_material", """{"media_id":"cover-media-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/add", """{"media_id":"draft-1"}"""),
                (HttpMethod.Post, "cgi-bin/draft/get", """{"news_item":[{"title":"Hello"}]}""")
            ]);
            var service = CreateService(handler, out var settings);
            settings.WeChatAppId = $"app-meta-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";
            settings.WeChatAuthor = "账号作者";

            var result = await service.PublishAsync(
                FormatResult("<p>正文</p>", "标题"),
                Path.GetTempPath(),
                "摘要",
                coverPath,
                metadata: new WeChatDraftMetadata {
                    Author = "文章作者",
                    ContentSourceUrl = "https://blog.example/p/post",
                    OpenComment = true,
                    FansOnlyComment = false,
                    CoverWidth = 1400,
                    CoverHeight = 700
                });

            result.Success.Should().BeTrue(result.ErrorMessage);
            var draft = handler.Requests.Should().ContainSingle(r =>
                r.Uri.AbsoluteUri.Contains("cgi-bin/draft/add", StringComparison.Ordinal)).Subject;
            draft.BodyText.Should().Contain("\"author\":\"文章作者\"");
            draft.BodyText.Should().NotContain("账号作者");
            draft.BodyText.Should().Contain("\"content_source_url\":\"https://blog.example/p/post\"");
            draft.BodyText.Should().Contain("\"need_open_comment\":1");
            draft.BodyText.Should().Contain("\"only_fans_can_comment\":0");
            draft.BodyText.Should().Contain("\"ratio\":\"2.35_1\"");
            draft.BodyText.Should().Contain("\"ratio\":\"1_1\"");
            draft.BodyText.Should().Contain("\"x1\":\"0.2500\"");
            draft.BodyText.Should().NotContain("show_cover_pic");
        }
        finally {
            File.Delete(coverPath);
        }
    }

    [Fact]
    public async Task PublishAsync_RejectsInvalidContentSourceUrlBeforeCallingWeChat() {
        var service = CreateService(new SequencingHandler([]), out var settings);
        settings.WeChatAppId = "app";
        settings.WeChatAppSecret = "secret";

        var result = await service.PublishAsync(
            FormatResult("<p>x</p>"),
            Path.GetTempPath(),
            "summary",
            coverPath: "missing.jpg",
            metadata: new WeChatDraftMetadata { ContentSourceUrl = "not a url" });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("阅读原文");
    }

    [Fact]
    public async Task PublishAsync_FailsEarlyWhenCoverMissing() {
        var service = CreateService(new SequencingHandler([]), out var settings);
        settings.WeChatAppId = "app";
        settings.WeChatAppSecret = "secret";

        var result = await service.PublishAsync(
            FormatResult("<p>x</p>"),
            Path.GetTempPath(),
            "summary",
            coverPath: null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("封面");
    }

    [Fact]
    public async Task PublishAsync_ReportsRelayAuthenticationFailureForHtml401() {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try {
            var handler = new SequencingHandler([
                (HttpMethod.Get, "cgi-bin/token", "<html>401 Authorization Required</html>")
            ], HttpStatusCode.Unauthorized);
            var service = CreateService(handler, out var settings);
            settings.WeChatAppId = $"app-auth-{Guid.NewGuid():N}";
            settings.WeChatAppSecret = "secret";

            var result = await service.PublishAsync(FormatResult("<p>x</p>"), Path.GetTempPath(), "summary", coverPath);

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("中转服务认证失败（HTTP 401）");
            result.ErrorMessage.Should().Contain("Basic");
        }
        finally {
            File.Delete(coverPath);
        }
    }

    private static WeChatFormatResult FormatResult(string html, string title = "Hello") => new() {
        Title = title,
        Html = html,
        WordCount = 10,
        Theme = new WeChatTheme("test", "Test", "#000", "#111", "#fff", "#222")
    };

    private static WeChatDraftPublishApplicationService CreateService(
        SequencingHandler handler,
        out AppSettings settings) {
        settings = new AppSettings();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(WeChatHttpClientRegistration.ApiClientName))
            .Returns(() => {
                var client = new HttpClient(handler, disposeHandler: false) {
                    BaseAddress = new Uri(WeChatHttpClientRegistration.OfficialApiBaseUrl)
                };
                return client;
            });
        factory.Setup(f => f.CreateClient(WeChatHttpClientRegistration.ImageDownloadClientName))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return new WeChatDraftPublishApplicationService(settings, factory.Object);
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string ContentType, string BodyText, byte[] BodyBytes);

    private sealed class SequencingHandler : HttpMessageHandler {
        private readonly Queue<(HttpMethod Method, string PathContains, string ResponseJson)> _responses;
        private readonly HttpStatusCode _responseStatus;
        public List<CapturedRequest> Requests { get; } = [];
        public byte[]? RemoteImageBytes { get; init; }

        public SequencingHandler(IEnumerable<(HttpMethod Method, string PathContains, string ResponseJson)> responses,
            HttpStatusCode responseStatus = HttpStatusCode.OK) {
            _responses = new Queue<(HttpMethod Method, string PathContains, string ResponseJson)>(responses);
            _responseStatus = responseStatus;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            var bodyText = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var contentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;
            var bodyBytes = request.Content == null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, contentType, bodyText, bodyBytes));

            if (_responses.Count == 0) {
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            var expected = _responses.Dequeue();
            if (request.Method != expected.Method ||
                request.RequestUri!.AbsoluteUri.Contains(expected.PathContains, StringComparison.Ordinal) == false) {
                throw new InvalidOperationException(
                    $"Expected {expected.Method} containing '{expected.PathContains}', got {request.Method} {request.RequestUri}");
            }

            if (expected.PathContains == "images.example" && RemoteImageBytes != null) {
                // Extensionless URLs and generic MIME types must be decoded from actual bytes.
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(RemoteImageBytes) };
            }
            return new HttpResponseMessage(_responseStatus) {
                Content = new StringContent(expected.ResponseJson, Encoding.UTF8,
                    _responseStatus == HttpStatusCode.OK ? "application/json" : "text/html")
            };
        }
    }
}
