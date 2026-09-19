using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StarBlogPublisher.Services.AI;

public enum AIModelCatalogStatus {
    Available,
    MissingApiKey,
    Unsupported,
    Failed
}

public enum AIModelSource {
    Provider,
    Recommended,
    SavedConfiguration
}

/// <summary>A provider-neutral model descriptor. Price values are USD per million tokens when supplied by the provider.</summary>
public sealed record AIModelDescriptor(
    string Id,
    string? DisplayName = null,
    int? ContextLength = null,
    decimal? InputPricePerMillionUsd = null,
    decimal? OutputPricePerMillionUsd = null,
    AIModelSource Source = AIModelSource.Provider) {

    public string Name => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;

    public string PriceSummary => InputPricePerMillionUsd is null && OutputPricePerMillionUsd is null
        ? "价格未由当前提供商目录返回"
        : $"输入 ${InputPricePerMillionUsd ?? 0m:0.####}/百万 tokens · 输出 ${OutputPricePerMillionUsd ?? 0m:0.####}/百万 tokens";

    public string SourceSummary => Source switch {
        AIModelSource.Provider => "\u63d0\u4f9b\u5546\u76ee\u5f55",
        AIModelSource.Recommended => "\u5185\u7f6e\u63a8\u8350",
        _ => "\u5df2\u4fdd\u5b58\u3001\u672a\u9a8c\u8bc1"
    };

    public string ContextSummary => ContextLength is null
        ? "\u4e0a\u4e0b\u6587\u7a97\u53e3\u672a\u77e5"
        : $"{ContextLength:N0} tokens \u4e0a\u4e0b\u6587\u7a97\u53e3";

    public override string ToString() => Id;
}

public sealed record AIModelCatalogResult(
    IReadOnlyList<AIModelDescriptor> Models,
    AIModelCatalogStatus Status,
    string Message,
    DateTimeOffset FetchedAt) {

    public bool IsAvailable => Status == AIModelCatalogStatus.Available;
}

/// <summary>
/// Queries a provider catalog without conflating a failed remote request with local recommendations.
/// The OpenRouter catalog includes live context-window and price metadata; ordinary OpenAI-compatible
/// catalogs provide model IDs only.
/// </summary>
public sealed class AIModelCatalogService {
    private readonly HttpMessageHandler? _handler;

    public AIModelCatalogService(HttpMessageHandler? handler = null) {
        _handler = handler;
    }

    public async Task<AIModelCatalogResult> GetModelsAsync(
        AIProviderInfo provider,
        string? apiKey,
        string? apiBase,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(provider);
        var now = DateTimeOffset.UtcNow;
        var suggested = CreateRecommendedModels(provider);

        if (provider.ModelCatalogKind == AIModelCatalogKind.Unsupported) {
            return new AIModelCatalogResult(
                suggested,
                AIModelCatalogStatus.Unsupported,
                "当前应用的通用 OpenAI 兼容适配器不支持该提供商的原生模型目录。请选择兼容端点或等待原生适配器。",
                now);
        }

        if (string.IsNullOrWhiteSpace(apiKey)) {
            return new AIModelCatalogResult(
                suggested,
                AIModelCatalogStatus.MissingApiKey,
                "未填写 API Key；仅显示内置推荐模型，尚未验证可用性。",
                now);
        }

        var endpoint = string.IsNullOrWhiteSpace(apiBase) ? provider.DefaultApiBase : apiBase.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri)) {
            return new AIModelCatalogResult(suggested, AIModelCatalogStatus.Failed, "API Base URL 无效。", now);
        }

        try {
            using var client = CreateHttpClient();
            client.Timeout = TimeSpan.FromSeconds(GetTimeoutSeconds());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            var modelsEndpoint = new Uri($"{baseUri.AbsoluteUri.TrimEnd('/')}/models");
            using var response = await client.GetAsync(modelsEndpoint, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) {
                return new AIModelCatalogResult(
                    suggested,
                    AIModelCatalogStatus.Failed,
                    $"模型目录请求失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}。仅显示内置推荐模型。",
                    now);
            }

            var models = ParseModels(content, provider.ModelCatalogKind)
                .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (models.Length == 0) {
                return new AIModelCatalogResult(
                    suggested,
                    AIModelCatalogStatus.Failed,
                    "模型目录未返回可用模型；仅显示内置推荐模型。",
                    now);
            }

            return new AIModelCatalogResult(
                models,
                AIModelCatalogStatus.Available,
                provider.SupportsPriceCatalog
                    ? $"已从提供商目录加载 {models.Length} 个模型及可用价格信息。"
                    : $"已从提供商目录加载 {models.Length} 个模型。该接口未提供统一价格信息。",
                now);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception ex) {
            return new AIModelCatalogResult(
                suggested,
                AIModelCatalogStatus.Failed,
                $"模型目录请求异常：{ex.Message}。仅显示内置推荐模型。",
                now);
        }
    }

    private HttpClient CreateHttpClient() {
        if (_handler != null) {
            return new HttpClient(_handler, disposeHandler: false);
        }

        var handler = new HttpClientHandler();
        var settings = AppSettings.Instance;
        if (settings.UseProxy && !string.IsNullOrWhiteSpace(settings.ProxyHost) && settings.ProxyPort > 0) {
            handler.Proxy = new WebProxy($"{settings.ProxyType}://{settings.ProxyHost}:{settings.ProxyPort}");
            handler.UseProxy = true;
        }

        return new HttpClient(handler, disposeHandler: true);
    }

    private static int GetTimeoutSeconds() {
        var timeout = AppSettings.Instance.ProxyTimeout;
        return timeout > 0 ? timeout : 30;
    }

    private static IReadOnlyList<AIModelDescriptor> CreateRecommendedModels(AIProviderInfo provider) =>
        provider.DefaultModels
            .Select(model => new AIModelDescriptor(model, Source: AIModelSource.Recommended))
            .ToArray();

    private static IEnumerable<AIModelDescriptor> ParseModels(string content, AIModelCatalogKind kind) {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) {
            return [];
        }

        var models = new List<AIModelDescriptor>();
        foreach (var item in data.EnumerateArray()) {
            if (!item.TryGetProperty("id", out var idElement) || string.IsNullOrWhiteSpace(idElement.GetString())) {
                continue;
            }

            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var contextLength = item.TryGetProperty("context_length", out var contextElement) && contextElement.TryGetInt32(out var context)
                ? (int?)context
                : null;
            decimal? input = null;
            decimal? output = null;
            if (kind == AIModelCatalogKind.OpenRouter && item.TryGetProperty("pricing", out var pricing)) {
                input = ReadPricePerMillion(pricing, "prompt");
                output = ReadPricePerMillion(pricing, "completion");
            }

            models.Add(new AIModelDescriptor(idElement.GetString()!, name, contextLength, input, output));
        }

        return models;
    }

    private static decimal? ReadPricePerMillion(JsonElement pricing, string name) {
        if (!pricing.TryGetProperty(name, out var value)) {
            return null;
        }

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var perToken)
            ? perToken * 1_000_000m
            : null;
    }
}
