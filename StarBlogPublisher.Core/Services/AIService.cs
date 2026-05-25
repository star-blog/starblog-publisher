using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace StarBlogPublisher.Services;

/// <summary>
/// 大模型服务
/// <para>https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-chat-app</para>
/// </summary>
public class AiService {
    private static AiService? _instance;
    private IChatClient? _chatClient;
    private readonly ILogger _logger;

    public static AiService Instance {
        get {
            _instance ??= new AiService();
            return _instance;
        }
    }

    private AiService(ILogger? logger = null) {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        TryInitializeClient();

        // 订阅设置变更事件
        AppSettings.Instance.SettingsChanged += (_, _) => {
            TryInitializeClient();
        };
    }

    internal AiService(IChatClient chatClient) {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _chatClient = chatClient;
    }

    private bool TryInitializeClient() {
        var settings = AppSettings.Instance;

        if (!settings.EnableAI) {
            _chatClient = null;
            _logger.LogInformation("AI is disabled; skipping chat client initialization");
            return false;
        }

        var provider = AIProviderInfo.GetProvider(settings.AIProvider);
        var key = settings.AIKey?.Trim();
        var model = settings.AIModel?.Trim();

        if (provider == null) {
            _chatClient = null;
            _logger.LogWarning("AI provider not found: {Provider}", settings.AIProvider);
            return false;
        }

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model)) {
            _chatClient = null;
            _logger.LogInformation("AI settings incomplete; skipping chat client initialization");
            return false;
        }

        var endpointValue = settings.AIProvider.Equals("custom", StringComparison.OrdinalIgnoreCase)
            ? settings.AIApiBase?.Trim()
            : provider.DefaultApiBase;

        if (string.IsNullOrWhiteSpace(endpointValue) || !Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)) {
            _chatClient = null;
            _logger.LogInformation("AI endpoint is missing or invalid; skipping chat client initialization");
            return false;
        }

        _logger.LogInformation("InitializeChatClient, endpoint: {Endpoint}", endpoint);

        _chatClient = new OpenAIClient(
            new ApiKeyCredential(key),
            new OpenAIClientOptions {
                Endpoint = endpoint
            }
        ).GetChatClient(model).AsIChatClient();

        return true;
    }

    public IChatClient ChatClient => EnsureChatClient();

    public bool IsConfigured => _chatClient != null || TryInitializeClient();

    private IChatClient EnsureChatClient() {
        if (_chatClient != null) {
            return _chatClient;
        }

        if (TryInitializeClient() && _chatClient != null) {
            return _chatClient;
        }

        throw new InvalidOperationException("AI 功能未正确配置，请先在设置中启用 AI 并填写可用的 API Key、模型和接口地址。");
    }

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