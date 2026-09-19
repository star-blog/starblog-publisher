using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
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
                FormatResult("<p>no images</p>", "团队 Web 开发规范"),
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
        }
        finally {
            File.Delete(coverPath);
        }
    }

    [Fact]
    public async Task PublishAsync_UploadsContentImagesViaUploadimg() {
        var coverPath = Path.Combine(Path.GetTempPath(), $"starblog-cover-{Guid.NewGuid():N}.jpg");
        var imageDir = Path.Combine(Path.GetTempPath(), $"starblog-imgs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(imageDir);
        var contentImagePath = Path.Combine(imageDir, "pic.png");
        await File.WriteAllBytesAsync(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
        await File.WriteAllBytesAsync(contentImagePath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
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
                FormatResult("""<p><img src="pic.png" alt="x"></p>"""),
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
            contentUpload.BodyText.Should().Contain("filename=\"media.png\"");
            contentUpload.BodyText.Should().Contain("Content-Type: image/png");
        }
        finally {
            File.Delete(coverPath);
            Directory.Delete(imageDir, recursive: true);
        }
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

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string ContentType, string BodyText);

    private sealed class SequencingHandler : HttpMessageHandler {
        private readonly Queue<(HttpMethod Method, string PathContains, string ResponseJson)> _responses;
        public List<CapturedRequest> Requests { get; } = [];

        public SequencingHandler(IEnumerable<(HttpMethod Method, string PathContains, string ResponseJson)> responses) {
            _responses = new Queue<(HttpMethod Method, string PathContains, string ResponseJson)>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            var bodyText = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var contentType = request.Content?.Headers.ContentType?.ToString() ?? string.Empty;
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, contentType, bodyText));

            if (_responses.Count == 0) {
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            var expected = _responses.Dequeue();
            if (request.Method != expected.Method ||
                request.RequestUri!.AbsoluteUri.Contains(expected.PathContains, StringComparison.Ordinal) == false) {
                throw new InvalidOperationException(
                    $"Expected {expected.Method} containing '{expected.PathContains}', got {request.Method} {request.RequestUri}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(expected.ResponseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
