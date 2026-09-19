using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StarBlogPublisher.Models;

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
            client.Timeout = GetTimeout(settings);
            ConfigureApiClient(client, settings.CurrentWeChatAccount);
        }).ConfigurePrimaryHttpMessageHandler(() => CreateHandler(settings));

        services.AddHttpClient(ImageDownloadClientName, client => {
            client.Timeout = GetTimeout(settings);
        }).ConfigurePrimaryHttpMessageHandler(() => CreateHandler(settings));

        return services;
    }

    /// <summary>Returns a normalized HTTP(S) base URL, falling back to the official endpoint.</summary>
    public static Uri GetApiBaseAddress(AppSettings settings) {
        return GetApiBaseAddress(settings.CurrentWeChatAccount);
    }

    /// <summary>Returns the configured base address for an individual account.</summary>
    public static Uri GetApiBaseAddress(WeChatAccountProfile account) {
        return TryGetApiBaseAddress(account.ApiBaseUrl, out var apiBaseAddress)
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

    /// <summary>Configures an API client for the supplied account without mutating global settings.</summary>
    public static void ConfigureApiClient(HttpClient client, WeChatAccountProfile account) {
        client.BaseAddress = GetApiBaseAddress(account);
        client.DefaultRequestHeaders.Authorization = null;
        ApplyAuthorizationHeader(client, account);
    }

    /// <summary>Applies the optional Authorization header required by a WeChat relay.</summary>
    internal static void ApplyAuthorizationHeader(HttpClient client, WeChatAccountProfile account) {
        var authorization = account.ApiAuthorization.Trim();
        if (authorization.Length == 0) return;

        // AuthenticationHeaderValue validates the value and prevents control characters
        // from being persisted as an HTTP header. The value includes its scheme, for
        // example: "Bearer relay-token".
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)) {
            throw new InvalidOperationException("微信 API Authorization 头格式无效。");
        }

        client.DefaultRequestHeaders.Authorization = header;
    }

    /// <summary>Checks whether an optional Authorization header value is valid.</summary>
    public static bool IsValidApiAuthorization(string? value) {
        return string.IsNullOrWhiteSpace(value) ||
               AuthenticationHeaderValue.TryParse(value.Trim(), out _);
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
