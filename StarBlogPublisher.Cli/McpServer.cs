using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
        return 0;
    }
}
