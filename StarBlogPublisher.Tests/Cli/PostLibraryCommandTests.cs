using FluentAssertions;
using StarBlogPublisher.Cli.Commands;

namespace StarBlogPublisher.Tests.Cli;

public class PostLibraryCommandTests {
    [Fact]
    public void ListCommand_ParsesFilters() {
        var parseResult = PostCommand.Build().Parse("list --page 2 --page-size 10 --search hello --category 3 --published false");
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void UpdateCommand_RequiresFile() {
        var parseResult = PostCommand.Build().Parse("update ./a.md --id abc --category 1");
        parseResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DeleteAndPullCommands_Parse() {
        PostCommand.Build().Parse("delete post-1").Errors.Should().BeEmpty();
        PostCommand.Build().Parse("pull post-1 --out ./a.md").Errors.Should().BeEmpty();
    }
}

public class CategoryMutationCommandTests {
    [Fact]
    public void UpdateAndDeleteCommands_Parse() {
        CategoryCommand.Build().Parse("update --id 2 --name 新分类 --parent-id 1 --visible false").Errors.Should().BeEmpty();
        CategoryCommand.Build().Parse("delete --id 2").Errors.Should().BeEmpty();
    }
}
