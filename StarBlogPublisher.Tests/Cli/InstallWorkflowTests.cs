using FluentAssertions;
using StarBlogPublisher.Cli.Install;

namespace StarBlogPublisher.Tests.Cli;

public class InstallWorkflowTests {
    [Fact]
    public void Run_McpClaudeCode_FallsBackToFileWriteWithoutSerializerMetadataDependency() {
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalRunner = ClaudeCodeMcpInstaller.CliRunner;
        var tempHome = Path.Combine(Path.GetTempPath(), $"starblog-install-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempHome);
        Environment.SetEnvironmentVariable("HOME", tempHome);
        ClaudeCodeMcpInstaller.CliRunner = new CommandNotFoundClaudeCodeCliRunner();

        try {
            var request = new InstallRequest(
                "claude-code",
                StarBlogInstallConstants.DefaultSkillName,
                "starblog-test",
                "starblog",
                ["mcp"]);

            var exitCode = InstallWorkflow.Run(InstallFeature.Mcp, request);
            var configPath = Path.Combine(tempHome, ".claude.json");
            var content = File.ReadAllText(configPath);

            exitCode.Should().Be(0);
            File.Exists(configPath).Should().BeTrue();
            content.Should().Contain("\"mcpServers\"");
            content.Should().Contain("\"starblog-test\"");
            content.Should().Contain("\"command\": \"starblog\"");
            content.Should().Contain("\"args\"");
            content.Should().Contain("\"mcp\"");
        }
        finally {
            ClaudeCodeMcpInstaller.CliRunner = originalRunner;
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) {
                Directory.Delete(tempHome, recursive: true);
            }
        }
    }

    [Fact]
    public void Run_McpClaudeCode_WhenServerExists_UsesRemoveThenAddViaClaudeCli() {
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var originalRunner = ClaudeCodeMcpInstaller.CliRunner;
        var tempHome = Path.Combine(Path.GetTempPath(), $"starblog-install-{Guid.NewGuid():N}");
        var fakeRunner = new SequenceClaudeCodeCliRunner(
            new ClaudeCliCommandResult(false, false, "MCP server starblog-test already exists in user config"),
            new ClaudeCliCommandResult(true, false, "Removed MCP server starblog-test"),
            new ClaudeCliCommandResult(true, false, "Added stdio MCP server starblog-test"));

        Directory.CreateDirectory(tempHome);
        Environment.SetEnvironmentVariable("HOME", tempHome);
        ClaudeCodeMcpInstaller.CliRunner = fakeRunner;

        try {
            var request = new InstallRequest(
                "claude-code",
                StarBlogInstallConstants.DefaultSkillName,
                "starblog-test",
                "starblog",
                ["mcp"]);

            var exitCode = InstallWorkflow.Run(InstallFeature.Mcp, request);

            exitCode.Should().Be(0);
            fakeRunner.Calls.Should().ContainInOrder(
                "add:starblog-test:starblog mcp",
                "remove:starblog-test",
                "add:starblog-test:starblog mcp");
            File.Exists(Path.Combine(tempHome, ".claude.json")).Should().BeFalse();
        }
        finally {
            ClaudeCodeMcpInstaller.CliRunner = originalRunner;
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) {
                Directory.Delete(tempHome, recursive: true);
            }
        }
    }

    private sealed class CommandNotFoundClaudeCodeCliRunner : IClaudeCodeCliRunner {
        public ClaudeCliCommandResult RunBuildAdd(InstallRequest request) => new(false, true, "claude not found");

        public ClaudeCliCommandResult RunRemove(string serverName) => new(false, true, "claude not found");
    }

    private sealed class SequenceClaudeCodeCliRunner : IClaudeCodeCliRunner {
        private readonly Queue<ClaudeCliCommandResult> _results;

        public SequenceClaudeCodeCliRunner(params ClaudeCliCommandResult[] results) {
            _results = new Queue<ClaudeCliCommandResult>(results);
        }

        public List<string> Calls { get; } = [];

        public ClaudeCliCommandResult RunBuildAdd(InstallRequest request) {
            Calls.Add($"add:{request.McpServerName}:{request.ExecutableCommand} {string.Join(" ", request.ExecutableArgs)}");
            return _results.Dequeue();
        }

        public ClaudeCliCommandResult RunRemove(string serverName) {
            Calls.Add($"remove:{serverName}");
            return _results.Dequeue();
        }
    }
}