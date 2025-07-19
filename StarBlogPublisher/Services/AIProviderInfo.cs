using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace StarBlogPublisher.Services;

public class AIProviderInfo {
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DefaultApiBase { get; set; }
    public string DefaultModel { get; set; }
    public List<string> DefaultModels { get; set; } = new List<string>();

    private static readonly List<AIProviderInfo> Providers = [
        // https://platform.openai.com/docs/pricing
        new AIProviderInfo {
            Name = "openai",
            DisplayName = "OpenAI",
            Description = "OpenAI的GPT系列模型，包括GPT-4o和GPT-o1等",
            DefaultApiBase = "https://api.openai.com/v1",
            DefaultModel = "gpt-4o",
            DefaultModels = [
                "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", 
                "gpt-4.5-preview", 
                "gpt-4o", "gpt-4o-mini", 
                "gpt-4o-audio-preview", "gpt-4o-realtime-preview",
                "gpt-4o-mini-audio-preview", "gpt-4o-mini-realtime-preview",
                "o1", "o1-pro", "o1-mini",
                "o3", "o3-mini",
                "o4-mini"
            ]
        },

        // https://docs.anthropic.com/en/docs/about-claude/pricing
        new AIProviderInfo {
            Name = "claude",
            DisplayName = "Claude",
            Description = "Anthropic的Claude系列模型，包括Claude 3.5和Claude 3.7等",
            DefaultApiBase = "https://api.anthropic.com",
            DefaultModel = "claude-3.5-sonnet",
            DefaultModels = ["claude-3.7-sonnet", "claude-3.5-sonnet", "claude-3-haiku", "claude-3-opus"]
        },

        // https://docs.x.ai/docs/models#models-and-pricing
        new AIProviderInfo {
            Name = "grok",
            DisplayName = "Grok",
            Description = "X公司(原Twitter)开发的Grok AI模型",
            DefaultApiBase = "https://api.grok.x.ai/v1",
            DefaultModel = "grok-1",
            DefaultModels = ["grok-3-latest", "grok-3-fast-latest", "grok-3-mini-latest", "grok-3-mini-fast-latest"]
        },

        // https://ai.google.dev/gemini-api/docs/models
        new AIProviderInfo {
            Name = "gemini",
            DisplayName = "Google Gemini",
            Description = "Google的Gemini系列模型，包括Gemini Pro和Gemini Ultra等",
            DefaultApiBase = "https://generativelanguage.googleapis.com",
            DefaultModel = "gemini-1.5-pro",
            DefaultModels = ["gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-flash", "gemini-2.0-flash-lite"]
        },

        // https://api-docs.deepseek.com/zh-cn/quick_start/pricing
        new AIProviderInfo {
            Name = "deepseek",
            DisplayName = "DeepSeek",
            Description = "DeepSeek的AI模型，包括DeepSeek-V3和DeepSeek-R1等",
            DefaultApiBase = "https://api.deepseek.com/v1",
            DefaultModel = "deepseek-chat",
            DefaultModels = ["deepseek-chat", "deepseek-coder"]
        },
        // https://platform.moonshot.cn/docs/pricing/chat
        new AIProviderInfo {
          Name  = "kimi",
          DisplayName = "Moonshot Kimi",
          Description = "Moonshot的AI模型，最近很火的 Kimi-K2",
          DefaultApiBase = "https://api.moonshot.cn/v1",
          DefaultModel = "moonshot-v1-8k",
          DefaultModels = [
              "kimi-latest-8k","kimi-latest-32k","kimi-latest-128k",
              "kimi-k2-0711-preview",
              "moonshot-v1-8k","moonshot-v1-32k","moonshot-v1-128k",
          ]
        },
        // https://open.bigmodel.cn/console/modelcenter/square
        new AIProviderInfo {
            Name = "zhipu",
            DisplayName = "清华智谱AI",
            Description = "清华智谱的AI模型，可以申请完全免费模型接口，具有代表性的模型是 ChatGLM",
            DefaultApiBase = "https://open.bigmodel.cn/api/paas/v4",
            DefaultModel = "glm-4-flash",
            DefaultModels = ["glm-z1-flash", "glm-4-flash-250414", "glm-4-flashx", "glm-4-flash", "glm-4v-flash"]
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
    public async Task<(List<string> Models, bool Success, string ErrorMessage)> GetModelsAsync(string apiKey,
        string apiBase = null) {
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