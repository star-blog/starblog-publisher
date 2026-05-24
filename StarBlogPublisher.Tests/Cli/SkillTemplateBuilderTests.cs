using FluentAssertions;
using StarBlogPublisher.Cli.Install;

namespace StarBlogPublisher.Tests.Cli;

public class SkillTemplateBuilderTests {
    [Fact]
    public void Build_ShouldIncludeRequiredFrontmatterAndSkillBody() {
        var content = SkillTemplateBuilder.Build("starblog-publisher");

        content.Should().Contain("name: starblog-publisher");
        content.Should().Contain("description:");
        content.Should().Contain("# StarBlog Publisher AI Skill");
        content.Should().Contain("优先使用 MCP");
    }
}