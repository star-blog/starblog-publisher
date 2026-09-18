using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// Normalizes local and remote cover images for the WeChat draft API.
/// The API accepts JPG/PNG files, while public image services may return a variety
/// of source formats and dimensions.
/// </summary>
public sealed class WeChatCoverImageService {
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private const int MaxCoverBytes = 2 * 1024 * 1024;
    private readonly AppSettings _settings;

    public WeChatCoverImageService(AppSettings settings) {
        _settings = settings;
    }

    public async Task<string> PrepareLocalAsync(string path, int width, int height, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            throw new InvalidOperationException("找不到要使用的封面图片");
        }

        await using var stream = File.OpenRead(path);
        return await NormalizeAsync(stream, width, height, cancellationToken);
    }

    public async Task<string> DownloadAndPrepareAsync(Uri source, int width, int height, CancellationToken cancellationToken = default) {
        if (!string.Equals(source.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("封面 URL 必须使用 HTTP 或 HTTPS");
        }

        using var client = CreateHttpClient();
        using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxDownloadBytes) {
            throw new InvalidOperationException("在线图片不能超过 10 MB");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        var totalBytes = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) {
            totalBytes += read;
            if (totalBytes > MaxDownloadBytes) {
                throw new InvalidOperationException("在线图片不能超过 10 MB");
            }
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        bounded.Position = 0;
        return await NormalizeAsync(bounded, width, height, cancellationToken);
    }

    private async Task<string> NormalizeAsync(Stream input, int width, int height, CancellationToken cancellationToken) {
        using var image = await Image.LoadAsync(input, cancellationToken);
        image.Mutate(context => context.Resize(new ResizeOptions {
            Size = new Size(width, height),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));

        var directory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "wechat-cover");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, $"cover-{Guid.NewGuid():N}.jpg");

        await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = 88 }, cancellationToken);
        if (new FileInfo(outputPath).Length > MaxCoverBytes) {
            await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = 72 }, cancellationToken);
        }
        if (new FileInfo(outputPath).Length > MaxCoverBytes) {
            File.Delete(outputPath);
            throw new InvalidOperationException("处理后的封面图片仍超过公众号 2 MB 限制");
        }

        return outputPath;
    }

    private HttpClient CreateHttpClient() {
        var handler = new HttpClientHandler();
        if (_settings.UseProxy && !string.IsNullOrWhiteSpace(_settings.ProxyHost)) {
            handler.Proxy = new WebProxy($"{_settings.ProxyType}://{_settings.ProxyHost}:{_settings.ProxyPort}");
            handler.UseProxy = true;
        }

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(_settings.BackendTimeout) };
    }
}
