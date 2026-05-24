using FluentAssertions;
using StarBlogPublisher.Cli.Install;

namespace StarBlogPublisher.Tests.Cli;

public class InstallWorkflowTests {
    [Fact]
    public void Run_McpClaudeCode_WritesClaudeConfigWithoutSerializerMetadataDependency() {
        var originalHome = Environment.GetEnvironmentVariable("HOME");
        var tempHome = Path.Combine(Path.GetTempPath(), $"starblog-install-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempHome);
        Environment.SetEnvironmentVariable("HOME", tempHome);

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
            Environment.SetEnvironmentVariable("HOME", originalHome);
            if (Directory.Exists(tempHome)) {
                Directory.Delete(tempHome, recursive: true);
            }
        }
    }
}