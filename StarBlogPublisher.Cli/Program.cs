using System.CommandLine;
using StarBlogPublisher.Cli.Commands;

namespace StarBlogPublisher.Cli;

class Program {
    static async Task<int> Main(string[] args) {
        // MCP 模式
        if (args.Length > 0 && args[0] == "mcp") {
            return await McpServer.RunAsync();
        }

        // CLI 模式
        var rootCommand = new RootCommand("StarBlog Publisher CLI - 博客发布命令行工具");
        rootCommand.Subcommands.Add(AuthCommand.Build());
        rootCommand.Subcommands.Add(CategoryCommand.Build());
        rootCommand.Subcommands.Add(PostCommand.Build());
        rootCommand.Subcommands.Add(AiCommand.Build());
        rootCommand.Subcommands.Add(InstallCommand.Build());

        return rootCommand.Parse(args).Invoke();
    }
}
