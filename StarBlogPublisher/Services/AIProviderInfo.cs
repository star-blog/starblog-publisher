using System.Collections.Generic;

namespace StarBlogPublisher.Services;

public class AIProviderInfo {
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DefaultApiBase { get; set; }

    private static readonly List<AIProviderInfo> _providers = new() {
        new AIProviderInfo {
            Name = "OpenAI",
            DisplayName = "OpenAI",
            Description = "OpenAI的GPT系列模型，包括GPT-3.5和GPT-4等",
            DefaultApiBase = "https://api.openai.com/v1"
        },
        new AIProviderInfo {
            Name = "Claude",
            DisplayName = "Claude",
            Description = "Anthropic的Claude系列模型，包括Claude 2和Claude Instant等",
            DefaultApiBase = "https://api.anthropic.com"
        },
        new AIProviderInfo {
            Name = "DeepSeek",
            DisplayName = "DeepSeek",
            Description = "DeepSeek的AI模型，包括DeepSeek-7B和DeepSeek-67B等",
            DefaultApiBase = "https://api.deepseek.com/v1"
        },
        new AIProviderInfo {
            Name = "自定义",
            DisplayName = "自定义",
            Description = "自定义AI提供商，可以配置自己的API地址",
            DefaultApiBase = ""
        }
    };

    public static List<AIProviderInfo> GetProviders() => _providers;

    public static AIProviderInfo? GetProvider(string name) =>
        _providers.Find(p => p.Name == name);

    public static List<string> GetProviderNames() =>
        _providers.ConvertAll(p => p.Name);
}