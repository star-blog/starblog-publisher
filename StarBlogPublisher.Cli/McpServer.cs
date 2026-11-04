using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StarBlogPublisher.Cli.Tools;

namespace StarBlogPublisher.Cli;

/// <summary>
/// MCP Server 入口
/// 使用 stdio 传输，所有日志输出到 stderr，stdout 仅用于协议消息
/// </summary>
public static class McpServer {
    public static async Task<int> RunAsync() {
        var builder = Host.CreateApplicationBuilder();

        // 所有日志输出到 stderr，避免污染 MCP 协议的 stdout
        builder.Logging.AddConsole(consoleLogOptions => {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        RegisterTools(builder.Services
            .AddMcpServer()
            .WithStdioServerTransport());

        await builder.Build().RunAsync();
        return 0;
    }

    internal static IMcpServerBuilder RegisterTools(IMcpServerBuilder builder) =>
        // Name the argument so the SDK selects the type-list overload, rather than treating Type[] as one tool instance.
        builder.WithTools(toolTypes: new[] {
                typeof(AuthTools),
                typeof(CategoryTools),
                typeof(PostTools),
                typeof(AiTools)
            }, serializerOptions: null);
}
