using FluentAssertions;
using StarBlogPublisher.Cli.Commands;

namespace StarBlogPublisher.Tests.Cli;

public class AuthCommandTests {
    [Fact]
    public void LoginCommand_WithoutCredentials_DoesNotFailAtParseTime() {
        var command = AuthCommand.Build();

        var parseResult = command.Parse("login");

        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LoginCommand_WithExplicitCredentials_DoesNotFailAtParseTime() {
        var command = AuthCommand.Build();

        var parseResult = command.Parse("login --username user --password pass");

        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LogoutCommand_WithClearCredentialsFlag_DoesNotFailAtParseTime() {
        var command = AuthCommand.Build();

        var parseResult = command.Parse("logout --clear-credentials");

        parseResult.Errors.Should().BeEmpty();
    }
}