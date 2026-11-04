using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using PublisherMcpServer = StarBlogPublisher.Cli.McpServer;

namespace StarBlogPublisher.Tests.Cli;

public class McpServerRegistrationTests {
    [Fact]
    public void RegisterTools_RegistersCallableAuthCategoryAndPostTools() {
        var services = new ServiceCollection();
        services.AddLogging();
        PublisherMcpServer.RegisterTools(services.AddMcpServer());
        using var provider = services.BuildServiceProvider();

        var names = provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToList();

        names.Should().Contain(["auth_login", "category_list", "category_create", "category_update", "category_delete",
            "post_list", "post_get", "post_publish", "post_update", "post_delete", "post_pull"]);
    }
}
