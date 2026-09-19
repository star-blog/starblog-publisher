using FluentAssertions;
using StarBlogPublisher.Services.AI;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class AiMetadataDraftViewModelTests {
    [Fact]
    public void Constructor_InitializesTheFirstCandidatesAndEditableMetadata() {
        var draft = new ArticleMetadataDraft(
            [new TitleCandidate("Primary title"), new TitleCandidate("Alternative title")],
            "A concise summary",
            ["dotnet", "testing"],
            ["primary-title", "alternative-title"],
            ["Review facts before publishing"]);

        var viewModel = new AiMetadataDraftViewModel(draft, _ => { });

        viewModel.TitleCandidates.Should().HaveCount(2);
        viewModel.SelectedTitleCandidate.Should().Be(draft.Titles![0]);
        viewModel.Summary.Should().Be("A concise summary");
        viewModel.Tags.Should().Be("dotnet, testing");
        viewModel.SlugCandidates.Should().ContainInOrder("primary-title", "alternative-title");
        viewModel.SelectedSlug.Should().Be("primary-title");
        viewModel.Warnings.Should().Be("Review facts before publishing");
    }

    [Fact]
    public void ApplyCommand_PassesTheUserSelectionAndRequestsClose() {
        SelectedArticleMetadata? applied = null;
        var closeRequested = false;
        var viewModel = new AiMetadataDraftViewModel(
            new ArticleMetadataDraft(
                [new TitleCandidate("First title"), new TitleCandidate("Chosen title")],
                "Initial summary",
                ["initial"],
                ["first-title", "chosen-title"]),
            metadata => applied = metadata);
        viewModel.SelectedTitleCandidate = viewModel.TitleCandidates[1];
        viewModel.Summary = "Edited summary";
        viewModel.Tags = "dotnet, avalonia";
        viewModel.SelectedSlug = "chosen-title";
        viewModel.CloseRequested += () => closeRequested = true;

        viewModel.ApplyCommand.Execute(null);

        applied.Should().Be(new SelectedArticleMetadata(
            "Chosen title", "Edited summary", "dotnet, avalonia", "chosen-title"));
        closeRequested.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyCandidates_UsesEmptyEditableValues() {
        var viewModel = new AiMetadataDraftViewModel(
            new ArticleMetadataDraft(null, null, null, null, null),
            _ => { });

        viewModel.SelectedTitleCandidate.Should().BeNull();
        viewModel.SelectedSlug.Should().BeNull();
        viewModel.Summary.Should().BeEmpty();
        viewModel.Tags.Should().BeEmpty();
        viewModel.Warnings.Should().BeEmpty();
    }
}
