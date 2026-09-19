using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public class PublishResultViewModelTests {
    [Fact]
    public void Constructor_UsesPublishedMarkdownAndPostMetadata() {
        var result = PublishResult.Ok(
            new BlogPost { Id = "post-42", Title = "Published title", Content = "Server content" },
            "https://blog.example/p/published-title",
            "Processed markdown");

        var viewModel = new PublishResultViewModel(result);

        viewModel.ArticleId.Should().Be("post-42");
        viewModel.ArticleTitle.Should().Be("Published title");
        viewModel.ArticleUrl.Should().Be("https://blog.example/p/published-title");
        viewModel.MarkdownContent.Should().Be("Processed markdown");
    }

    [Fact]
    public void Constructor_UsesPostContentWhenPublishedMarkdownIsMissing() {
        var result = PublishResult.Ok(new BlogPost { Content = "Server content" });

        var viewModel = new PublishResultViewModel(result);

        viewModel.MarkdownContent.Should().Be("Server content");
        viewModel.ArticleId.Should().BeEmpty();
        viewModel.ArticleTitle.Should().BeEmpty();
        viewModel.ArticleUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task CopyCommands_WithEmptyContent_ReportThatNothingCanBeCopied() {
        var viewModel = new PublishResultViewModel(PublishResult.Fail("Publish failed"));

        await viewModel.CopyUrlCommand.ExecuteAsync(null);
        viewModel.StatusMessage.Should().Contain("URL");

        await viewModel.CopyMarkdownCommand.ExecuteAsync(null);
        viewModel.StatusMessage.Should().Contain("Markdown");

        await viewModel.CopyTitleCommand.ExecuteAsync(null);
        viewModel.StatusMessage.Should().Contain("标题");
    }
}
