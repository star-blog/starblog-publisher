using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace StarBlogPublisher.Services.Application;

/// <summary>A JPEG cover kept at its source aspect ratio, ready for WeChat crop windows.</summary>
public readonly record struct PreparedWeChatCover(string Path, int Width, int Height);

/// <summary>
/// Normalizes local and remote cover images for the WeChat draft API.
/// The uploaded file keeps its aspect ratio; headline and secondary crops are sent as cover_info.
/// </summary>
public sealed class WeChatCoverImageService {
    private const int MaxDownloadBytes = 10 * 1024 * 1024;
    private const int MaxCoverBytes = 2 * 1024 * 1024;
    private const int MaxEdge = 1920;
    /// <summary>Neutral fetch size so both 2.35:1 and 1:1 center crops retain image content.</summary>
    public const int RandomSourceWidth = 1200;
    public const int RandomSourceHeight = 900;
    private readonly IHttpClientFactory _httpClientFactory;

    public WeChatCoverImageService(IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PreparedWeChatCover> PrepareLocalAsync(string path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            throw new InvalidOperationException("找不到要使用的封面图片");
        }

        await using var stream = File.OpenRead(path);
        return await NormalizeAsync(stream, cancellationToken);
    }

    public async Task<PreparedWeChatCover> DownloadAndPrepareAsync(Uri source, CancellationToken cancellationToken = default) {
        if (!string.Equals(source.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("封面 URL 必须使用 HTTP 或 HTTPS");
        }

        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ImageDownloadClientName);
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
        return await NormalizeAsync(bounded, cancellationToken);
    }

    private static async Task<PreparedWeChatCover> NormalizeAsync(Stream input, CancellationToken cancellationToken) {
        using var image = await Image.LoadAsync(input, cancellationToken);
        if (image.Width > MaxEdge || image.Height > MaxEdge) {
            var scale = Math.Min(MaxEdge / (double)image.Width, MaxEdge / (double)image.Height);
            var width = Math.Max(1, (int)Math.Round(image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(image.Height * scale));
            image.Mutate(context => context.Resize(width, height));
        }

        var directory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "wechat-cover");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, $"cover-{Guid.NewGuid():N}.jpg");
        await SaveUnderLimitAsync(image, outputPath, cancellationToken);
        return new PreparedWeChatCover(outputPath, image.Width, image.Height);
    }

    private static async Task SaveUnderLimitAsync(Image image, string outputPath, CancellationToken cancellationToken) {
        foreach (var quality in new[] { 88, 72, 60 }) {
            await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = quality }, cancellationToken);
            if (new FileInfo(outputPath).Length <= MaxCoverBytes) return;
        }

        while (new FileInfo(outputPath).Length > MaxCoverBytes && image.Width > 640 && image.Height > 640) {
            image.Mutate(context => context.Resize(Math.Max(1, image.Width * 3 / 4), Math.Max(1, image.Height * 3 / 4)));
            await image.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = 72 }, cancellationToken);
        }

        if (new FileInfo(outputPath).Length > MaxCoverBytes) {
            File.Delete(outputPath);
            throw new InvalidOperationException("处理后的封面图片仍超过公众号 2 MB 限制");
        }
    }
}
