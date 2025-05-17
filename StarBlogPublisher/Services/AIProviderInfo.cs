using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using FluentResults;

namespace StarBlogPublisher.Services;

public class AIProviderInfo {
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DefaultApiBase { get; set; }
    public string DefaultModel { get; set; }
    public List<string> DefaultModels { get; set; } = new List<string>();

    private static readonly List<AIProviderInfo> Providers = [
        new AIProviderInfo {
            Name = "openai",
            DisplayName = "OpenAI",
            Description = "OpenAI的GPT系列模型，包括GPT-4o和GPT-o1等",
            DefaultApiBase = "https://api.openai.com/v1",
            DefaultModel = "gpt-4o",
            DefaultModels = ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"]
        },

        new AIProviderInfo {
            Name = "claude",
            DisplayName = "Claude",
            Description = "Anthropic的Claude系列模型，包括Claude 3.5和Claude 3.7等",
            DefaultApiBase = "https://api.anthropic.com",
            DefaultModel = "claude-3.5-sonnet",
            DefaultModels = ["claude-3.5-sonnet", "claude-3-haiku", "claude-3-opus", "claude-3.7-sonnet"]
        },

        new AIProviderInfo {
            Name = "deepseek",
            DisplayName = "DeepSeek",
            Description = "DeepSeek的AI模型，包括DeepSeek-V3和DeepSeek-R1等",
            DefaultApiBase = "https://api.deepseek.com/v1",
            DefaultModel = "deepseek-chat",
            DefaultModels = ["deepseek-chat", "deepseek-coder"]
        },

        new AIProviderInfo {
            Name = "zhipu",
            DisplayName = "清华智谱AI",
            Description = "清华智谱的AI模型，可以申请完全免费模型接口，具有代表性的模型是 ChatGLM",
            DefaultApiBase = "https://open.bigmodel.cn/api/paas/v4",
            DefaultModel = "glm-4-flash",
            DefaultModels = ["glm-4-flash", "glm-4", "glm-3-turbo"]
        },

        new AIProviderInfo {
            Name = "custom",
            DisplayName = "自定义",
            Description = "自定义AI提供商，可以配置自己的API地址",
            DefaultApiBase = "",
            DefaultModel = "",
            DefaultModels = []
        }
    ];

    public static List<AIProviderInfo> GetProviders() => Providers;

    public static AIProviderInfo? GetProvider(string name) =>
        Providers.Find(p => p.Name == name);

    public static AIProviderInfo? GetByDisplayName(string displayName) =>
        Providers.Find(p => p.DisplayName == displayName);

    public static List<string> GetProviderNames() =>
        Providers.ConvertAll(p => p.Name);

    /// <summary>
    /// 获取模型列表
    /// </summary>
    /// <param name="apiKey">API密钥</param>
    /// <param name="apiBase">API基础地址</param>
    /// <returns>包含模型列表和状态的元组：(模型列表, 是否成功, 错误信息)</returns>
    public async Task<(List<string> Models, bool Success, string ErrorMessage)> GetModelsAsync(string apiKey, string apiBase = null) {
        try {
            // 如果未提供API密钥，直接返回默认模型
            if (string.IsNullOrEmpty(apiKey)) {
                return (DefaultModels, false, "未提供API密钥");
            }

            var baseUrl = !string.IsNullOrEmpty(apiBase) ? apiBase : DefaultApiBase;

            // 创建HttpClient并配置代理
            var handler = new HttpClientHandler();
            var settings = AppSettings.Instance;

            // 如果启用了代理，配置代理
            if (settings.UseProxy && !string.IsNullOrEmpty(settings.ProxyHost) && settings.ProxyPort > 0) {
                var proxyUri = $"{settings.ProxyType}://{settings.ProxyHost}:{settings.ProxyPort}";
                handler.Proxy = new WebProxy(proxyUri);
                handler.UseProxy = true;
            }

            using var client = new HttpClient(handler) {
                Timeout = TimeSpan.FromSeconds(settings.ProxyTimeout > 0 ? settings.ProxyTimeout : 30)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/models");

            if (response.IsSuccessStatusCode) {
                var content = await response.Content.ReadAsStringAsync();
                var modelsData = JsonSerializer.Deserialize<ModelsResponse>(content);

                if (modelsData?.Data != null && modelsData.Data.Count > 0) {
                    var modelList = modelsData.Data.ConvertAll(m => m.Id);
                    return (modelList, true, string.Empty);
                }
            }

            // API调用成功但返回失败状态码或无数据
            string errorMessage = $"获取模型列表失败：{response.ReasonPhrase}";
            return (DefaultModels, false, errorMessage);
        }
        catch (Exception ex) {
            string errorMessage = $"获取模型列表报错：{ex.Message}";
            Console.WriteLine(errorMessage);
            return (DefaultModels, false, errorMessage);
        }
    }

    private class ModelsResponse {
        public List<ModelInfo> Data { get; set; }
    }

    private class ModelInfo {
        public string Id { get; set; }
    }
}