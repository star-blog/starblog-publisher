using System.CommandLine;
using StarBlogPublisher.Cli.Install;

namespace StarBlogPublisher.Cli.Commands;

public static class InstallCommand {
    public static Command Build() {
        var command = new Command("install", "安装 StarBlog Publisher 的 skills 或 MCP 配置到 AI Agent");

        command.Subcommands.Add(BuildSkillsCommand());
        command.Subcommands.Add(BuildMcpCommand());

        return command;
    }

    private static Command BuildSkillsCommand() {
        var skillsCommand = new Command("skills", "安装 Skill 到目标 Agent");
        var agentOption = BuildAgentOption();
        var skillNameOption = new Option<string>("--name") {
            Description = "Skill 名称",
            DefaultValueFactory = _ => StarBlogInstallConstants.DefaultSkillName,
        };

        skillsCommand.Options.Add(agentOption);
        skillsCommand.Options.Add(skillNameOption);

        skillsCommand.SetAction(parseResult => {
            var agentId = parseResult.GetValue(agentOption);
            var skillName = parseResult.GetValue(skillNameOption) ?? StarBlogInstallConstants.DefaultSkillName;

            var options = new InstallRequest(
                agentId,
                skillName,
                StarBlogInstallConstants.DefaultMcpServerName,
                StarBlogInstallConstants.DefaultExecutableCommand,
                ["mcp"]);

            return InstallWorkflow.Run(InstallFeature.Skills, options);
        });

        return skillsCommand;
    }

    private static Command BuildMcpCommand() {
        var mcpCommand = new Command("mcp", "安装 MCP Server 配置到目标 Agent");
        var agentOption = BuildAgentOption();
        var serverNameOption = new Option<string>("--name") {
            Description = "MCP Server 名称",
            DefaultValueFactory = _ => StarBlogInstallConstants.DefaultMcpServerName,
        };
        var commandOption = new Option<string>("--command") {
            Description = "启动 StarBlog MCP Server 的命令",
            DefaultValueFactory = _ => StarBlogInstallConstants.DefaultExecutableCommand,
        };
        var argsOption = new Option<string[]>("--args") {
            Description = "传给 MCP 命令的参数，默认是 mcp",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => ["mcp"],
        };

        mcpCommand.Options.Add(agentOption);
        mcpCommand.Options.Add(serverNameOption);
        mcpCommand.Options.Add(commandOption);
        mcpCommand.Options.Add(argsOption);

        mcpCommand.SetAction(parseResult => {
            var agentId = parseResult.GetValue(agentOption);
            var serverName = parseResult.GetValue(serverNameOption) ?? StarBlogInstallConstants.DefaultMcpServerName;
            var commandValue = parseResult.GetValue(commandOption) ?? StarBlogInstallConstants.DefaultExecutableCommand;
            var argsValue = parseResult.GetValue(argsOption) ?? ["mcp"];

            if (argsValue.Length == 0) {
                argsValue = ["mcp"];
            }

            var options = new InstallRequest(
                agentId,
                StarBlogInstallConstants.DefaultSkillName,
                serverName,
                commandValue,
                argsValue);

            return InstallWorkflow.Run(InstallFeature.Mcp, options);
        });

        return mcpCommand;
    }

    private static Option<string?> BuildAgentOption() {
        return new Option<string?>("--agent") {
            Description = "目标 Agent ID，例如 claude-code、codex、openclaw。不传时进入交互式选择。",
        };
    }
}