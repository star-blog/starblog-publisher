using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public sealed class ArticleCoverIntegrationTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "article-cover-tests", Guid.NewGuid().ToString("N"));
    private MainWindowViewModel Shell() => new(Mock.Of<IHttpClientFactory>(), initializeSession: false, workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));
    private async Task<string> Article(string name = "post.md") {
        Directory.CreateDirectory(_directory); var path = Path.Combine(_directory, name);
        await File.WriteAllTextAsync(path, "# 文章\n\n![配图](photo.png)"); return path;
    }
    [Fact] public async Task OpeningStudioIsClean_AndBothEntrypointsShareArticleSession() {
        var shell = Shell(); await shell.Workspace.OpenPathAsync(await Article()); var doc = shell.PublishPage;
        doc.ShowCoverStudioCommand.Execute(null);
        var studio = (CoverStudioViewModel)doc.ActiveStackPage!;
        doc.IsDocumentDirty.Should().BeFalse(); studio.ArticleImages.Should().HaveCount(1);
        shell.WeChatPage.SyncFrom(doc); shell.WeChatPage.ShowCoverStudioCommand.Execute(null);
        var second = (CoverStudioViewModel)shell.WeChatPage.ActiveStackPage!;
        second.Session.Should().BeSameAs(studio.Session); second.ArticleImages.Should().HaveCount(1);
        second.CoverTitle = "共享草稿"; studio.CoverTitle.Should().Be("共享草稿"); doc.IsDocumentDirty.Should().BeTrue();
        doc.NavigateBreadcrumbAt(0); shell.WeChatPage.NavigateBreadcrumbAt(0);
        await doc.SaveDocumentAsync(); await shell.Workspace.CloseActiveCommand.ExecuteAsync(null);
    }
    [Fact] public async Task SaveReopen_PreservesDraftAndAppliedIndependently_AndSwitchClearsWeChat() {
        var shell = Shell(); var path = await Article(); await shell.Workspace.OpenPathAsync(path); var doc = shell.PublishPage;
        doc.CoverSession.Change(doc.CoverSession.Draft with { Title = "已应用" }); await doc.CoverSession.ApplyAsync();
        doc.CoverSession.Change(doc.CoverSession.Draft with { Title = "未应用的修改" });
        (await doc.SaveDocumentAsync()).Should().BeTrue(); doc.IsDocumentDirty.Should().BeFalse();
        await shell.Workspace.CloseActiveCommand.ExecuteAsync(null); await shell.Workspace.OpenPathAsync(path);
        shell.PublishPage.CoverSession.State!.Applied!.Title.Should().Be("已应用");
        shell.PublishPage.CoverSession.Draft.Title.Should().Be("未应用的修改");
        shell.WeChatPage.UsePreparedCover((await shell.PublishPage.CoverSession.PrepareAppliedAsync())!.Value, "test");
        await shell.Workspace.OpenPathAsync(await Article("other.md")); shell.WeChatPage.SyncFrom(shell.PublishPage);
        shell.WeChatPage.CoverPath.Should().BeEmpty();
        await shell.Workspace.CloseAllCommand.ExecuteAsync(null);
    }
    [Fact] public async Task UnsavedCoverRecovery_RestoresContentAndPreservesFileConflict() {
        var shell = Shell(); shell.Workspace.NewDocumentCommand.Execute(null); var doc = shell.PublishPage;
        doc.ArticleContent = "# 尚未保存的文章"; doc.CoverSession.Change(new() { Title = "恢复的封面" });
        await Task.Delay(1300);
        shell.Workspace.CoverRecovery.Entries.Should().ContainSingle();
        var restarted = Shell(); await restarted.Workspace.RestoreCoverEditsCommand.ExecuteAsync(null);
        restarted.PublishPage.ArticleContent.Should().Be(doc.ArticleContent);
        restarted.PublishPage.CoverSession.Draft.Title.Should().Be("恢复的封面");
        restarted.PublishPage.CurrentFilePath.Should().BeNull();
        // Close through the article-owned lifecycle without showing a save dialog in this unit test.
        doc.CoverSession.Close(); restarted.PublishPage.CoverSession.Close();
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
