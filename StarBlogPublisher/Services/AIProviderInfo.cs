using System.Collections.Generic;

namespace StarBlogPublisher.Services;

public class AIProviderInfo {
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DefaultApiBase { get; set; }
    public string DefaultModel { get; set; }

    private static readonly List<AIProviderInfo> Providers = [
        new AIProviderInfo {
            Name = "openai",
            DisplayName = "OpenAI",
            Description = "OpenAI的GPT系列模型，包括GPT-4o和GPT-o1等",
            DefaultApiBase = "https://api.openai.com/v1",
            DefaultModel = "gpt-4o"
        },

        new AIProviderInfo {
            Name = "claude",
            DisplayName = "Claude",
            Description = "Anthropic的Claude系列模型，包括Claude 3.5和Claude 3.7等",
            DefaultApiBase = "https://api.anthropic.com",
            DefaultModel = "claude-3.5-sonnet"
        },

        new AIProviderInfo {
            Name = "deepseek",
            DisplayName = "DeepSeek",
            Description = "DeepSeek的AI模型，包括DeepSeek-V3和DeepSeek-R1等",
            DefaultApiBase = "https://api.deepseek.com/v1",
            DefaultModel = "deepseek-chat"
        },

        new AIProviderInfo {
            Name = "custom",
            DisplayName = "自定义",
            Description = "自定义AI提供商，可以配置自己的API地址",
            DefaultApiBase = "",
            DefaultModel = ""
        }
    ];

    public static List<AIProviderInfo> GetProviders() => Providers;

    public static AIProviderInfo? GetProvider(string name) =>
        Providers.Find(p => p.Name == name);
    
    public static AIProviderInfo? GetByDisplayName(string displayName) =>
        Providers.Find(p => p.DisplayName == displayName);

    public static List<string> GetProviderNames() =>
        Providers.ConvertAll(p => p.Name);
}