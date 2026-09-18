using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 将微信公众号排版后的文章上传为草稿箱图文。
/// </summary>
public sealed class WeChatDraftPublishApplicationService {
    private const int MaxContentImageBytes = 1024 * 1024;
    private const int MaxCoverImageBytes = 2 * 1024 * 1024;
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static WeChatAccessToken? _tokenCache;
    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;

    public WeChatDraftPublishApplicationService(AppSettings settings, IHttpClientFactory httpClientFactory) {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WeChatDraftPublishResult> PublishAsync(
        WeChatFormatResult formatResult,
        string sourceDirectory,
        string summary,
        string? coverPath,
        Action<int, string>? onProgress = null) {
        try {
            if (string.IsNullOrWhiteSpace(_settings.WeChatAppId) || string.IsNullOrWhiteSpace(_settings.WeChatAppSecret)) {
                return WeChatDraftPublishResult.Fail("请先在设置中配置微信公众号 AppId 和 AppSecret");
            }

            if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath)) {
                return WeChatDraftPublishResult.Fail("请先选择有效的公众号封面图");
            }

            onProgress?.Invoke(10, "正在获取微信公众号访问凭据...");
            var token = await GetAccessTokenAsync();

            onProgress?.Invoke(25, "正在上传正文图片...");
            var (html, imageUrls) = await UploadContentImagesAsync(formatResult.Html, sourceDirectory, token, onProgress);

            onProgress?.Invoke(75, "正在上传封面图...");
            var thumbMediaId = await UploadCoverAsync(coverPath, token);

            onProgress?.Invoke(88, "正在创建公众号草稿...");
            var draftMediaId = await CreateDraftAsync(
                token,
                formatResult.Title,
                html,
                thumbMediaId,
                _settings.WeChatAuthor,
                summary);

            onProgress?.Invoke(96, "正在校验公众号草稿...");
            await VerifyDraftAsync(token, draftMediaId);
            onProgress?.Invoke(100, "草稿已创建");

            return WeChatDraftPublishResult.Ok(draftMediaId, html, thumbMediaId, imageUrls);
        }
        catch (Exception ex) {
            return WeChatDraftPublishResult.Fail($"上传公众号草稿失败: {ex.Message}");
        }
    }

    private async Task<string> GetAccessTokenAsync() {
        var cacheKey = $"{WeChatHttpClientRegistration.GetApiBaseAddress(_settings)}:{_settings.WeChatAppId}:{_settings.WeChatAppSecret}";
        if (_tokenCache is { } cached && cached.Key == cacheKey && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5)) {
            return cached.Token;
        }

        await TokenLock.WaitAsync();
        try {
            if (_tokenCache is { } refreshedCache && refreshedCache.Key == cacheKey && refreshedCache.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5)) {
                return refreshedCache.Token;
            }

            using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ApiClientName);
            var url = "cgi-bin/token?grant_type=client_credential"
                + $"&appid={Uri.EscapeDataString(_settings.WeChatAppId)}"
                + $"&secret={Uri.EscapeDataString(_settings.WeChatAppSecret)}";
            using var response = await client.GetAsync(url);
            var document = await ReadResponseAsync(response);
            ThrowIfWeChatError(response, document, "获取 access token");

            var token = GetRequiredString(document, "access_token", "获取 access token");
            var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt32(out var seconds)
                ? seconds
                : 7200;
            _tokenCache = new WeChatAccessToken(cacheKey, token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
            return token;
        }
        finally {
            TokenLock.Release();
        }
    }

    private async Task<(string Html, IReadOnlyList<string> ImageUrls)> UploadContentImagesAsync(
        string html,
        string sourceDirectory,
        string token,
        Action<int, string>? onProgress) {
        var matches = Regex.Matches(html, @"<img\b(?<prefix>[^>]*?\bsrc\s*=\s*)(?<quote>[""'])(?<src>[^""']*)\k<quote>(?<suffix>[^>]*)>", RegexOptions.IgnoreCase);
        if (matches.Count == 0) return (html, Array.Empty<string>());

        var output = new StringBuilder();
        var urls = new List<string>();
        var cursor = 0;
        var completed = 0;
        foreach (Match match in matches) {
            output.Append(html, cursor, match.Index - cursor);
            var source = WebUtility.HtmlDecode(match.Groups["src"].Value);
            var replacement = match.Value;
            if (!source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !source.Contains("mmbiz.qpic.cn", StringComparison.OrdinalIgnoreCase)) {
                string? temporaryPath = null;
                try {
                    var imagePath = source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? temporaryPath = await DownloadExternalImageAsync(source)
                        : ResolveLocalImagePath(sourceDirectory, source);
                    if (imagePath == null || !File.Exists(imagePath)) {
                        throw new InvalidOperationException($"找不到正文图片: {source}");
                    }

                    ValidateImage(imagePath, MaxContentImageBytes, "正文图片");
                    var uploadedUrl = await UploadImageAsync("cgi-bin/media/uploadimg", token, imagePath, "url");
                    replacement = match.Value.Replace(match.Groups["src"].Value, WebUtility.HtmlEncode(uploadedUrl), StringComparison.Ordinal);
                    urls.Add(uploadedUrl);
                }
                finally {
                    if (temporaryPath != null) File.Delete(temporaryPath);
                }
            }

            completed++;
            onProgress?.Invoke(25 + (int)(completed * 45.0 / matches.Count), $"正在上传正文图片 ({completed}/{matches.Count})...");
            output.Append(replacement);
            cursor = match.Index + match.Length;
        }

        output.Append(html, cursor, html.Length - cursor);
        return (output.ToString(), urls);
    }

    private async Task<string> UploadCoverAsync(string coverPath, string token) {
        ValidateImage(coverPath, MaxCoverImageBytes, "封面图");
        return await UploadImageAsync("cgi-bin/material/add_material?type=thumb", token, coverPath, "media_id");
    }

    private async Task<string> UploadImageAsync(string endpoint, string token, string imagePath, string resultField) {
        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ApiClientName);
        using var form = new MultipartFormDataContent();
        await using var file = File.OpenRead(imagePath);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageMimeType(imagePath));
        form.Add(fileContent, "media", Path.GetFileName(imagePath));

        var separator = endpoint.Contains('?') ? "&" : "?";
        using var response = await client.PostAsync($"{endpoint}{separator}access_token={Uri.EscapeDataString(token)}", form);
        var document = await ReadResponseAsync(response);
        ThrowIfWeChatError(response, document, "上传图片");
        return GetRequiredString(document, resultField, "上传图片");
    }

    private async Task<string> CreateDraftAsync(
        string token,
        string title,
        string html,
        string thumbMediaId,
        string author,
        string summary) {
        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ApiClientName);
        var payload = new {
            articles = new[] {
                new {
                    title,
                    author,
                    digest = summary,
                    content = html,
                    thumb_media_id = thumbMediaId,
                    show_cover_pic = 0,
                    need_open_comment = 1,
                    only_fans_can_comment = 0
                }
            }
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"cgi-bin/draft/add?access_token={Uri.EscapeDataString(token)}", content);
        var document = await ReadResponseAsync(response);
        ThrowIfWeChatError(response, document, "创建公众号草稿");
        return GetRequiredString(document, "media_id", "创建公众号草稿");
    }

    private async Task VerifyDraftAsync(string token, string draftMediaId) {
        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ApiClientName);
        using var content = new StringContent(JsonSerializer.Serialize(new { media_id = draftMediaId }), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"cgi-bin/draft/get?access_token={Uri.EscapeDataString(token)}", content);
        var document = await ReadResponseAsync(response);
        ThrowIfWeChatError(response, document, "校验公众号草稿");
        if (!document.RootElement.TryGetProperty("news_item", out _)) {
            throw new InvalidOperationException("公众号未返回已创建的草稿内容");
        }
    }

    private async Task<string?> DownloadExternalImageAsync(string url) {
        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ImageDownloadClientName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var extension = response.Content.Headers.ContentType?.MediaType switch {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => throw new InvalidOperationException("正文图片仅支持 JPG 或 PNG 格式")
        };
        if (response.Content.Headers.ContentLength is > MaxContentImageBytes) {
            throw new InvalidOperationException("正文图片不能超过 1 MB");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"starblog-wechat-{Guid.NewGuid():N}{extension}");
        try {
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(tempPath);
            var buffer = new byte[81920];
            var totalBytes = 0;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0) {
                totalBytes += read;
                if (totalBytes > MaxContentImageBytes) throw new InvalidOperationException("正文图片不能超过 1 MB");
                await output.WriteAsync(buffer.AsMemory(0, read));
            }
            return tempPath;
        }
        catch {
            File.Delete(tempPath);
            throw;
        }
    }

    private static string? ResolveLocalImagePath(string sourceDirectory, string source) {
        var decodedPath = Uri.UnescapeDataString(source).Replace('/', Path.DirectorySeparatorChar);
        return Path.IsPathRooted(decodedPath)
            ? decodedPath
            : Path.GetFullPath(Path.Combine(sourceDirectory, decodedPath));
    }

    private static void ValidateImage(string imagePath, int maxBytes, string imageType) {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png") {
            throw new InvalidOperationException($"{imageType}仅支持 JPG 或 PNG 格式");
        }
        if (new FileInfo(imagePath).Length > maxBytes) {
            throw new InvalidOperationException($"{imageType}不能超过 {maxBytes / 1024 / 1024} MB");
        }
    }

    private static string GetImageMimeType(string imagePath) => Path.GetExtension(imagePath).ToLowerInvariant() == ".png"
        ? "image/png"
        : "image/jpeg";

    private static async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response) {
        var text = await response.Content.ReadAsStringAsync();
        try {
            return JsonDocument.Parse(text);
        }
        catch (JsonException) {
            throw new InvalidOperationException($"微信公众号接口返回了无效响应（HTTP {(int)response.StatusCode}）");
        }
    }

    private static void ThrowIfWeChatError(HttpResponseMessage response, JsonDocument document, string action) {
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"{action}失败（HTTP {(int)response.StatusCode}）");
        }
        if (document.RootElement.TryGetProperty("errcode", out var errorCode) && errorCode.GetInt32() != 0) {
            var message = document.RootElement.TryGetProperty("errmsg", out var errorMessage) ? errorMessage.GetString() : "未知错误";
            throw new InvalidOperationException($"{action}失败（{errorCode.GetInt32()}: {message}）");
        }
    }

    private static string GetRequiredString(JsonDocument document, string propertyName, string action) {
        if (document.RootElement.TryGetProperty(propertyName, out var value) && !string.IsNullOrWhiteSpace(value.GetString())) {
            return value.GetString()!;
        }
        throw new InvalidOperationException($"{action}未返回 {propertyName}");
    }

    private sealed record WeChatAccessToken(string Key, string Token, DateTimeOffset ExpiresAt);
}

/// <summary>
/// 公众号草稿上传结果。
/// </summary>
public sealed class WeChatDraftPublishResult {
    public bool Success { get; init; }
    public string? DraftMediaId { get; init; }
    public string? FormattedHtml { get; init; }
    public string? CoverMediaId { get; init; }
    public IReadOnlyList<string> ContentImageUrls { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }

    public static WeChatDraftPublishResult Ok(string draftMediaId, string html, string coverMediaId, IReadOnlyList<string> contentImageUrls) => new() {
        Success = true,
        DraftMediaId = draftMediaId,
        FormattedHtml = html,
        CoverMediaId = coverMediaId,
        ContentImageUrls = contentImageUrls
    };

    public static WeChatDraftPublishResult Fail(string errorMessage) => new() { ErrorMessage = errorMessage };
}
