using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace StarBlogPublisher.Services;

public class AIProviderInfo
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DefaultApiBase { get; set; }
    public string DefaultModel { get; set; }
    public List<string> DefaultModels { get; set; } = new List<string>();

    private static readonly List<AIProviderInfo> Providers = [
        // 所有内置服务商均使用 OpenAI Chat Completions 兼容接口，
        // 因此可以复用 AiService 中的 OpenAIClient 调用链。
        // https://platform.openai.com/docs/models
        new AIProviderInfo {
            Name = "openai", DisplayName = "OpenAI",
            Description = "OpenAI GPT 系列，适合通用写作、推理和代码任务",
            DefaultApiBase = "https://api.openai.com/v1", DefaultModel = "gpt-5.6-luna",
            DefaultModels = ["gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna", "gpt-5.6", "gpt-5.4", "gpt-5-mini", "gpt-4.1", "gpt-4.1-mini", "gpt-4o"]
        },

        // https://docs.anthropic.com/en/docs/about-claude/model-deprecations
        new AIProviderInfo {
            Name = "claude", DisplayName = "Claude",
            Description = "Anthropic Claude 系列，擅长长文写作、分析和代码",
            DefaultApiBase = "https://api.anthropic.com/v1", DefaultModel = "claude-sonnet-5",
            DefaultModels = ["claude-sonnet-5", "claude-opus-5", "claude-sonnet-4-6", "claude-opus-4-8", "claude-haiku-4-5-20251001"]
        },

        // https://docs.x.ai/developers/models
        new AIProviderInfo {
            Name = "grok", DisplayName = "Grok",
            Description = "xAI 的 Grok 系列，支持代码、推理和多模态对话",
            DefaultApiBase = "https://api.x.ai/v1", DefaultModel = "grok-4.6",
            DefaultModels = ["grok-4.6", "grok-4.5", "grok-4.3"]
        },

        // https://ai.google.dev/gemini-api/docs/models
        // https://ai.google.dev/gemini-api/docs/openai?hl=zh-cn
        new AIProviderInfo {
            Name = "gemini", DisplayName = "Google Gemini",
            Description = "Google Gemini 系列，适合多模态理解与通用生成",
            DefaultApiBase = "https://generativelanguage.googleapis.com/v1beta/openai/", DefaultModel = "gemini-3.8-flash",
            DefaultModels = ["gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-3.1-pro-preview", "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.5-flash-lite"]
        },

        // https://api-docs.deepseek.com/updates/
        new AIProviderInfo {
            Name = "deepseek", DisplayName = "DeepSeek",
            Description = "DeepSeek 系列，提供高性价比的通用与推理模型",
            DefaultApiBase = "https://api.deepseek.com/v1", DefaultModel = "deepseek-v4-flash",
            DefaultModels = ["deepseek-v4-flash", "deepseek-v4-pro"]
        },

        // https://help.aliyun.com/zh/model-studio/qwen-api-via-openai-chat-completions
        new AIProviderInfo {
            Name = "qwen", DisplayName = "阿里云百炼（通义千问）",
            Description = "阿里云百炼的 Qwen 系列，支持中文写作、多模态与推理",
            DefaultApiBase = "https://dashscope.aliyuncs.com/compatible-mode/v1", DefaultModel = "qwen3.8-flash",
            DefaultModels = ["qwen3.8-max", "qwen3.8-flash", "qwen3.7-max", "qwen3.7-plus", "qwen3.7-flash", "qwen3.6-plus", "qwen3.6-flash", "qwen3.5-plus", "qwen3.5-flash"]
        },

        // https://www.volcengine.com/docs/82379/1795150
        new AIProviderInfo {
            Name = "doubao", DisplayName = "火山方舟（豆包）",
            Description = "火山引擎方舟的豆包系列，适合中文内容生成、推理和多模态任务",
            DefaultApiBase = "https://ark.cn-beijing.volces.com/api/v3", DefaultModel = "doubao-seed-2-1-pro-260628",
            DefaultModels = ["doubao-seed-2-1-pro-260628", "doubao-seed-2-0-pro-260215", "doubao-seed-2-0-lite-260215", "doubao-seed-2-0-code-preview-260215"]
        },

        // https://platform.moonshot.cn/docs/
        new AIProviderInfo {
            Name = "kimi", DisplayName = "Moonshot Kimi",
            Description = "Moonshot 的 Kimi 系列，擅长长上下文、推理和中文写作",
            DefaultApiBase = "https://api.moonshot.cn/v1", DefaultModel = "kimi-k2.5",
            DefaultModels = ["kimi-k2.5", "kimi-k2-thinking-turbo", "kimi-k2-turbo-preview", "kimi-k2-0905-preview"]
        },

        // https://open.bigmodel.cn/
        new AIProviderInfo {
            Name = "zhipu", DisplayName = "智谱 AI（GLM）",
            Description = "智谱 GLM 系列，提供通用、推理、代码和视觉模型",
            DefaultApiBase = "https://open.bigmodel.cn/api/paas/v4", DefaultModel = "glm-5-turbo",
            DefaultModels = ["glm-5.2", "glm-5.1", "glm-5", "glm-5-turbo", "glm-4.7", "glm-4.6v"]
        },

        // https://platform.minimax.io/docs/api-reference/text-openai-api
        new AIProviderInfo {
            Name = "minimax", DisplayName = "MiniMax",
            Description = "MiniMax M 系列，适合长上下文、复杂推理和代码任务",
            DefaultApiBase = "https://api.minimax.io/v1", DefaultModel = "MiniMax-M2.7",
            DefaultModels = ["MiniMax-M2.7", "MiniMax-M2.7-highspeed", "MiniMax-M2.5", "MiniMax-M2.5-highspeed", "MiniMax-M2.1", "MiniMax-M2"]
        },

        // https://cloud.tencent.com/document/product/1729/111007
        new AIProviderInfo {
            Name = "hunyuan", DisplayName = "腾讯混元",
            Description = "腾讯混元系列，支持中文生成、推理和视觉理解",
            DefaultApiBase = "https://api.hunyuan.cloud.tencent.com/v1", DefaultModel = "hunyuan-turbos-latest",
            DefaultModels = ["hunyuan-turbos-latest", "hunyuan-a13b", "hunyuan-vision-1.5-instruct"]
        },

        // https://cloud.baidu.com/doc/qianfan/s/Hmh4suq26
        new AIProviderInfo {
            Name = "qianfan", DisplayName = "百度千帆",
            Description = "百度千帆平台，提供 ERNIE 和多家主流模型的兼容接口",
            DefaultApiBase = "https://qianfan.baidubce.com/v2", DefaultModel = "ernie-4.5-turbo-20260402",
            DefaultModels = ["ernie-4.5-turbo-20260402", "deepseek-v4-flash", "deepseek-v4-pro", "deepseek-v3.2", "glm-5.1", "glm-5", "kimi-k2.6"]
        },

        // https://docs.siliconflow.cn/docs/api/chat-completions-post
        new AIProviderInfo {
            Name = "siliconflow", DisplayName = "硅基流动",
            Description = "硅基流动聚合平台，可调用 DeepSeek、Qwen、Kimi 等开源模型",
            DefaultApiBase = "https://api.siliconflow.cn/v1", DefaultModel = "deepseek-ai/DeepSeek-V4-Flash",
            DefaultModels = ["deepseek-ai/DeepSeek-V4-Flash", "deepseek-ai/DeepSeek-V4-Pro", "Qwen/Qwen3.5-397B-A17B", "moonshotai/Kimi-K2.7-Code"]
        },

        // https://docs.mistral.ai/models/
        new AIProviderInfo {
            Name = "mistral", DisplayName = "Mistral AI",
            Description = "Mistral 的通用、推理和代码模型",
            DefaultApiBase = "https://api.mistral.ai/v1", DefaultModel = "mistral-medium-latest",
            DefaultModels = ["mistral-medium-latest", "mistral-large-latest", "mistral-small-latest", "devstral-latest"]
        },

        // https://console.groq.com/docs/models
        new AIProviderInfo {
            Name = "groq", DisplayName = "GroqCloud",
            Description = "GroqCloud 的超低延迟推理服务，托管 Llama、GPT-OSS 和 Qwen 等模型",
            DefaultApiBase = "https://api.groq.com/openai/v1", DefaultModel = "openai/gpt-oss-20b",
            DefaultModels = ["openai/gpt-oss-120b", "openai/gpt-oss-20b", "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "qwen/qwen3.8-27b", "groq/compound", "groq/compound-mini"]
        },

        // https://openrouter.ai/docs/quickstart
        new AIProviderInfo {
            Name = "openrouter", DisplayName = "OpenRouter",
            Description = "聚合数百种模型的统一接口；也可在验证 API Key 后获取完整可用模型列表",
            DefaultApiBase = "https://openrouter.ai/api/v1", DefaultModel = "~openai/gpt-latest",
            DefaultModels = ["~openai/gpt-latest", "~anthropic/claude-sonnet-latest", "~google/gemini-flash-latest", "openai/gpt-oss-120b", "deepseek/deepseek-v4-flash", "qwen/qwen3.8-flash"]
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
    public async Task<(List<string> Models, bool Success, string ErrorMessage)> GetModelsAsync(
        string apiKey,
        string apiBase = null)
    {
        try
        {
            // 如果未提供API密钥，直接返回默认模型
            if (string.IsNullOrEmpty(apiKey))
            {
                return (DefaultModels, false, "未提供API密钥");
            }

            var baseUrl = !string.IsNullOrEmpty(apiBase) ? apiBase : DefaultApiBase;

            // 创建HttpClient并配置代理
            var handler = new HttpClientHandler();
            var settings = AppSettings.Instance;

            // 如果启用了代理，配置代理
            if (settings.UseProxy && !string.IsNullOrEmpty(settings.ProxyHost) && settings.ProxyPort > 0)
            {
                var proxyUri = $"{settings.ProxyType}://{settings.ProxyHost}:{settings.ProxyPort}";
                handler.Proxy = new WebProxy(proxyUri);
                handler.UseProxy = true;
            }

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(settings.ProxyTimeout > 0 ? settings.ProxyTimeout : 30)
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/models");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var modelsData = JsonSerializer.Deserialize<ModelsResponse>(content);

                if (modelsData?.Data != null && modelsData.Data.Count > 0)
                {
                    var modelList = modelsData.Data.ConvertAll(m => m.Id);
                    return (modelList, true, string.Empty);
                }
            }

            // API调用成功但返回失败状态码或无数据
            string errorMessage = $"获取模型列表失败：{response.ReasonPhrase}";
            return (DefaultModels, false, errorMessage);
        }
        catch (Exception ex)
        {
            string errorMessage = $"获取模型列表报错：{ex.Message}";
            System.Diagnostics.Trace.TraceWarning(errorMessage);
            return (DefaultModels, false, errorMessage);
        }
    }

    private class ModelsResponse
    {
        public List<ModelInfo> Data { get; set; }
    }

    private class ModelInfo
    {
        public string Id { get; set; }
    }
}
