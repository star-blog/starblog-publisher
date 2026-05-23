using FluentAssertions;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Utils;

public class PromptBuilderTests {
    [Fact]
    public void Build_SingleParameter_SubstitutesCorrectly() {
        var result = PromptBuilder
            .Create("Hello {{name}}!")
            .AddParameter("name", "World")
            .Build();

        result.Should().Be("Hello World!");
    }

    [Fact]
    public void Build_MultipleParameters_SubstitutesAll() {
        var result = PromptBuilder
            .Create("Title: {{title}}, Content: {{content}}")
            .AddParameter("title", "Test")
            .AddParameter("content", "Body")
            .Build();

        result.Should().Be("Title: Test, Content: Body");
    }

    [Fact]
    public void Build_MissingParameter_LeavesPlaceholder() {
        var result = PromptBuilder
            .Create("Hello {{name}}, age {{age}}!")
            .AddParameter("name", "World")
            .Build();

        result.Should().Be("Hello World, age {{age}}!");
    }

    [Fact]
    public void Build_EmptyValue_SubstitutesWithEmpty() {
        var result = PromptBuilder
            .Create("Value: {{key}}")
            .AddParameter("key", "")
            .Build();

        result.Should().Be("Value: ");
    }

    [Fact]
    public void Build_NoParameters_ReturnsOriginal() {
        var template = "No placeholders here.";
        var result = PromptBuilder.Create(template).Build();

        result.Should().Be(template);
    }

    [Fact]
    public void Build_SameParameterTwice_SubstitutesBothOccurrences() {
        var result = PromptBuilder
            .Create("{{x}} and {{x}}")
            .AddParameter("x", "same")
            .Build();

        result.Should().Be("same and same");
    }

    [Fact]
    public void Create_ReturnsBuilder() {
        var builder = PromptBuilder.Create("template");
        builder.Should().NotBeNull();
    }
}
