using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace StarBlogPublisher.Services;

/// <summary>
/// 大模型服务
/// <para>https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-chat-app</para>
/// </summary>
public class AiService {
    private static AiService? _instance;
    private IChatClient _chatClient;

    public static AiService Instance {
        get {
            _instance ??= new AiService();
            return _instance;
        }
    }

    private AiService() {
        InitializeClient();
        
        // 订阅设置变更事件
        AppSettings.Instance.SettingsChanged += (_, _) => {
            InitializeClient();
        };
    }

    private void InitializeClient() {
        var settings = AppSettings.Instance;
        
        var provider = AIProviderInfo.GetProvider(settings.AIProvider);
        var key = settings.AIKey;
        var model = settings.AIModel;
        
        if (provider == null) {
            throw new ApplicationException("AI provider not found");
        }
        
        var endpoint = settings.AIProvider.ToLower() == "custom"
            ? new Uri(settings.AIApiBase)
            : new Uri(provider.DefaultApiBase);

        Console.WriteLine($"InitializeChatClient, endpoint: {endpoint}");
        
        _chatClient = new OpenAIClient(
            new ApiKeyCredential(key),
            new OpenAIClientOptions {
                Endpoint = endpoint
            }
        ).GetChatClient(model).AsIChatClient();
    }

    public IChatClient ChatClient => _chatClient;

    /// <summary>
    /// 生成文本
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <returns>生成的文本</returns>
    public async Task<string> GenerateTextAsync(string prompt) {
        try {
            var response = await ChatClient.GetResponseAsync(prompt);
            return response.Text;
        }
        catch (Exception ex) {
            throw new Exception($"AI文本生成失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 生成聊天回复
    /// </summary>
    /// <param name="messages">聊天历史记录</param>
    /// <returns>AI的回复</returns>
    public async Task<string> GenerateChatReplyAsync(params ChatMessage[] messages) {
        try {
            var response = await ChatClient.GetResponseAsync(messages);
            return response.Text;
        }
        catch (Exception ex) {
            throw new Exception($"AI聊天回复生成失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 流式生成文本
    /// </summary>
    /// <param name="prompt">提示词</param>
    /// <returns>生成的文本流</returns>
    public IAsyncEnumerable<ChatResponseUpdate> GenerateTextStreamAsync(string prompt) {
        try {
            return ChatClient.GetStreamingResponseAsync(prompt);
        }
        catch (Exception ex) {
            throw new Exception($"AI文本流生成失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 流式生成聊天回复
    /// </summary>
    /// <param name="messages">聊天历史记录</param>
    /// <returns>AI的回复流</returns>
    public IAsyncEnumerable<ChatResponseUpdate> GenerateChatReplyStreamAsync(params ChatMessage[] messages) {
        try {
            return ChatClient.GetStreamingResponseAsync(messages);
        }
        catch (Exception ex) {
            throw new Exception($"AI聊天回复流生成失败: {ex.Message}", ex);
        }
    }
}