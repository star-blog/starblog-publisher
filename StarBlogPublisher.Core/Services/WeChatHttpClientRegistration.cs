using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StarBlogPublisher.Services;

/// <summary>Registers the HTTP clients used by the WeChat publishing workflow.</summary>
public static class WeChatHttpClientRegistration {
    public const string ApiClientName = "WeChatApi";
    public const string ImageDownloadClientName = "WeChatImageDownload";
    public const string OfficialApiBaseUrl = "https://api.weixin.qq.com/";

    /// <summary>
    /// Adds pooled HTTP clients for WeChat API requests and remote image downloads.
    /// The API base address is read each time a client is created, so a saved setting
    /// takes effect for the next publishing operation without restarting the app.
    /// </summary>
    public static IServiceCollection AddWeChatHttpClients(this IServiceCollection services, AppSettings settings) {
        services.AddHttpClient(ApiClientName, client => {
            client.BaseAddress = GetApiBaseAddress(settings);
            client.Timeout = GetTimeout(settings);
        }).ConfigurePrimaryHttpMessageHandler(() => CreateHandler(settings));

        services.AddHttpClient(ImageDownloadClientName, client => {
            client.Timeout = GetTimeout(settings);
        }).ConfigurePrimaryHttpMessageHandler(() => CreateHandler(settings));

        return services;
    }

    /// <summary>Returns a normalized HTTP(S) base URL, falling back to the official endpoint.</summary>
    public static Uri GetApiBaseAddress(AppSettings settings) {
        return TryGetApiBaseAddress(settings.WeChatApiBaseUrl, out var apiBaseAddress)
            ? apiBaseAddress
            : new Uri(OfficialApiBaseUrl);
    }

    /// <summary>Validates and normalizes a configured API base URL.</summary>
    public static bool TryGetApiBaseAddress(string? value, out Uri apiBaseAddress) {
        if (string.IsNullOrWhiteSpace(value)) {
            apiBaseAddress = new Uri(OfficialApiBaseUrl);
            return true;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
            apiBaseAddress = null!;
            return false;
        }

        var builder = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty };
        var normalized = builder.Uri.AbsoluteUri.TrimEnd('/') + "/";
        apiBaseAddress = new Uri(normalized, UriKind.Absolute);
        return true;
    }

    private static TimeSpan GetTimeout(AppSettings settings) =>
        TimeSpan.FromSeconds(settings.BackendTimeout > 0 ? settings.BackendTimeout : 30);

    private static HttpMessageHandler CreateHandler(AppSettings settings) {
        var handler = new HttpClientHandler();
        if (settings.UseProxy && !string.IsNullOrWhiteSpace(settings.ProxyHost) && settings.ProxyPort > 0) {
            handler.Proxy = new WebProxy($"{settings.ProxyType}://{settings.ProxyHost}:{settings.ProxyPort}");
            handler.UseProxy = true;
        }

        return handler;
    }
}
