using FluentAssertions;
using StarBlogPublisher.Services.AI;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.AI;

public class ArticleMetadataDraftTests {
    [Fact]
    public void Normalize_RemovesInvalidAndDuplicateData() {
        var draft = new ArticleMetadataDraft(
            [
                new TitleCandidate("  A practical title  "),
                new TitleCandidate("a practical title"),
                new TitleCandidate("Second title")
            ],
            "  Summary  ",
            [" C# ", "c#", "", new string('a', 33), "Avalonia"],
            ["Hello World", "hello-world", "中文"],
            ["", "Needs a source"]);

        var result = draft.Normalize(AiApplicationService.CleanSlug);

        result.Titles.Should().HaveCount(2);
        result.Titles!.Select(item => item.Title).Should().Equal("A practical title", "Second title");
        result.Summary.Should().Be("Summary");
        result.Tags.Should().Equal("C#", "Avalonia");
        result.SlugCandidates.Should().Equal("elloorld", "hello-world");
        result.Warnings.Should().Equal("Needs a source");
    }

    [Fact]
    public void Normalize_LimitsCandidatesToSafeBounds() {
        var draft = new ArticleMetadataDraft(
            Enumerable.Range(1, 8).Select(number => new TitleCandidate($"Title {number}")).ToArray(),
            null,
            Enumerable.Range(1, 14).Select(number => $"tag-{number}").ToArray(),
            Enumerable.Range(1, 5).Select(number => $"slug-{number}").ToArray());

        var result = draft.Normalize(AiApplicationService.CleanSlug);

        result.Titles.Should().HaveCount(5);
        result.Tags.Should().HaveCount(10);
        result.SlugCandidates.Should().HaveCount(3);
    }
}
