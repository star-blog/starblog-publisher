using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public class ArticleWorkspaceViewModelTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "starblog-workspace-tests", Guid.NewGuid().ToString("N"));
    private MainWindowViewModel CreateShell() => new(Mock.Of<IHttpClientFactory>(), initializeSession: false,
        workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));

    private async Task<string> Article(string name, string content) {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    [Fact]
    public async Task OpeningAndSwitching_PreservesIndependentDocumentState_AndDeduplicatesPaths() {
        var workspace = CreateShell().Workspace;
        var firstPath = await Article("first.md", "# First");
        var secondPath = await Article("second.md", "# Second");
        await workspace.OpenPathAsync(firstPath);
        var first = workspace.CurrentDocument;
        first.ArticleContent = "# Edited first";
        first.ArticleTitle = "Custom first title";
        first.ArticleDescription = "First summary";
        first.SelectedCategory = new Category { Id = 42, Text = "Development" };
        first.EditorMode = MarkdownEditorMode.Preview;
        first.CaretOffset = 7;
        await workspace.OpenPathAsync(secondPath);
        workspace.CurrentDocument.ArticleKeywords = "second";
        workspace.CurrentDocument.SelectedCategory.Should().BeNull();
        workspace.CurrentDocument.PreviewUri!.AbsolutePath.Should().NotBe(first.PreviewUri!.AbsolutePath);

        await workspace.OpenPathAsync(Path.Combine(_directory, ".", "first.md"));

        workspace.Documents.Should().HaveCount(2);
        workspace.CurrentDocument.Should().BeSameAs(first);
        first.ArticleContent.Should().Be("# Edited first");
        first.ArticleTitle.Should().Be("Custom first title");
        first.ArticleDescription.Should().Be("First summary");
        first.SelectedCategory!.Id.Should().Be(42);
        first.ArticleKeywords.Should().BeEmpty();
        first.EditorMode.Should().Be(MarkdownEditorMode.Preview);
        first.CaretOffset.Should().Be(7);
        first.IsDocumentDirty.Should().BeTrue();
    }

    [Fact]
    public async Task Saving_RoundTripsMarkdownAndProperties_AndResetsDirtyState() {
        var shell = CreateShell();
        var path = await Article("saved.md", "# Original");
        await shell.Workspace.OpenPathAsync(path);
        var document = shell.PublishPage;
        document.IsDocumentDirty.Should().BeFalse();
        document.ArticleDescription = "摘要";
        document.IsDocumentDirty.Should().BeTrue();
        document.ArticleContent = "# 已修改\n\n正文";
        document.ArticleTitle = "发布标题";
        document.ArticleKeywords = "one, two";
        document.ArticleSlug = "saved-article";
        document.SelectedCategory = new Category { Id = 17, Text = "技术" };

        (await document.SaveDocumentAsync()).Should().BeTrue();
        document.IsDocumentDirty.Should().BeFalse();
        (await File.ReadAllTextAsync(path)).Should().Be(document.ArticleContent);
        File.Exists(path + ".starblog.json").Should().BeTrue();
        await shell.Workspace.CloseActiveCommand.ExecuteAsync(null);
        shell.Workspace.HasDocuments.Should().BeFalse();
        await shell.Workspace.OpenPathAsync(path);
        shell.PublishPage.ArticleTitle.Should().Be("发布标题");
        shell.PublishPage.ArticleDescription.Should().Be("摘要");
        shell.PublishPage.ArticleKeywords.Should().Be("one, two");
        shell.PublishPage.ArticleSlug.Should().Be("saved-article");
        shell.PublishPage.SelectedCategory!.Id.Should().Be(17);
        shell.PublishPage.IsDocumentDirty.Should().BeFalse();
    }

    [Fact]
    public async Task FailedOpen_DoesNotReplaceCurrentDocument() {
        var workspace = CreateShell().Workspace;
        await workspace.OpenPathAsync(await Article("valid.md", "# Keep me"));
        var current = workspace.CurrentDocument;
        await workspace.OpenPathAsync(Path.Combine(_directory, "missing.md"));
        workspace.CurrentDocument.Should().BeSameAs(current);
        workspace.Documents.Should().ContainSingle();
    }

    [Fact]
    public async Task FailedSave_KeepsUnsavedState() {
        var workspace = CreateShell().Workspace;
        var path = await Article("blocked.md", "original");
        await workspace.OpenPathAsync(path);
        Directory.CreateDirectory(path + ".starblog.json");
        workspace.CurrentDocument.ArticleContent = "edited";
        (await workspace.CurrentDocument.SaveDocumentAsync()).Should().BeFalse();
        workspace.CurrentDocument.IsDocumentDirty.Should().BeTrue();
    }

    [Fact]
    public async Task ClosingLastCleanDocument_ReturnsToWelcome_AndCanReopen() {
        var workspace = CreateShell().Workspace;
        var path = await Article("close.md", "hello");
        await workspace.OpenPathAsync(path);
        await workspace.CloseActiveCommand.ExecuteAsync(null);
        workspace.ActiveDocument.Should().BeNull();
        workspace.CurrentDocument.HasLoadedArticle.Should().BeFalse();
        await workspace.OpenRecentCommand.ExecuteAsync(path);
        workspace.HasDocuments.Should().BeTrue();
    }

    [Fact]
    public void NewDocument_IsUnsaved_AndHasIndependentState() {
        var workspace = CreateShell().Workspace;
        workspace.NewDocumentCommand.Execute(null);
        var first = workspace.CurrentDocument;
        first.ArticleContent = "unsaved";
        workspace.NewDocumentCommand.Execute(null);
        workspace.CurrentDocument.Should().NotBeSameAs(first);
        workspace.CurrentDocument.ArticleContent.Should().BeEmpty();
        first.IsDocumentDirty.Should().BeTrue();
        workspace.CurrentDocument.IsDocumentDirty.Should().BeTrue();
    }

    [Fact]
    public async Task ClosingSelectedTab_SelectsNeighbor_WhenSelectionControlClearsSelection() {
        var workspace = CreateShell().Workspace;
        await workspace.OpenPathAsync(await Article("first.md", "first"));
        var first = workspace.CurrentDocument;
        await workspace.OpenPathAsync(await Article("second.md", "second"));
        workspace.Documents.CollectionChanged += (_, e) => {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                workspace.ActiveDocument = null;
        };
        await workspace.CloseActiveCommand.ExecuteAsync(null);
        workspace.ActiveDocument.Should().BeSameAs(first);
    }

    [Fact]
    public async Task ConcurrentOpen_DoesNotCreateDuplicateTabs() {
        var workspace = CreateShell().Workspace;
        var path = await Article("concurrent.md", "content");
        await Task.WhenAll(workspace.OpenPathAsync(path), workspace.OpenPathAsync(path));
        workspace.Documents.Should().ContainSingle();
    }

    [Fact]
    public async Task RestoreSession_ReopensSavedFilesAndSelectedTab() {
        var workspace = CreateShell().Workspace;
        var one = await Article("one.md", "one");
        var two = await Article("two.md", "two");
        await workspace.OpenPathAsync(one);
        await workspace.OpenPathAsync(two);
        workspace.ActiveDocument = workspace.Documents[0];
        var restored = CreateShell().Workspace;
        await restored.RestoreSessionCommand.ExecuteAsync(null);
        restored.Documents.Should().HaveCount(2);
        restored.CurrentDocument.CurrentFilePath.Should().Be(one);
    }

    [Fact]
    public void History_RoundTripsSession_AndToleratesCorruptJson() {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "workspace.json");
        var store = new WorkspaceHistoryStore(path);
        var history = new WorkspaceHistory(["one.md", "two.md"], ["two.md"], "two.md");
        store.Save(history);
        store.Load().Should().BeEquivalentTo(history);
        File.WriteAllText(path, "not json");
        store.Load().OpenFiles.Should().BeEmpty();
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
