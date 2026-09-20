using FluentAssertions;
using Moq;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public sealed class ShellCommandTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "starblog-shell-tests", Guid.NewGuid().ToString("N"));
    private MainWindowViewModel CreateShell() => new(Mock.Of<IHttpClientFactory>(), initializeSession: false,
        workspaceHistoryPath: Path.Combine(_directory, "workspace.json"));

    [Fact]
    public async Task FileCommands_UseCurrentDocument_AndDisableWithoutDocument() {
        var shell = CreateShell();
        shell.GetCommand("file.save").Command.CanExecute(null).Should().BeFalse();
        await shell.GetCommand("file.new").Command.ExecuteAsync(null);
        shell.Workspace.Documents.Should().ContainSingle();
        shell.GetCommand("file.save").Command.CanExecute(null).Should().BeTrue();
        shell.GetCommand("publish.blog").Command.CanExecute(null).Should().BeFalse();
        shell.PublishPage.IsPublishing = true;
        shell.GetCommand("file.save").Command.CanExecute(null).Should().BeFalse();
        shell.PublishPage.IsPublishing = false;
        shell.ActivePage = shell.AboutPage;
        shell.GetCommand("file.save").Command.CanExecute(null).Should().BeFalse();
        await shell.GetCommand("file.new").Command.ExecuteAsync(null);
        shell.ActivePage.Should().BeSameAs(shell.Workspace);
        shell.Workspace.Documents.Should().HaveCount(2);
    }

    [Fact]
    public void Commands_HaveUniqueIdsAndShortcuts() {
        var shell = CreateShell();
        shell.ShellCommands.Select(c => c.Id).Should().OnlyHaveUniqueItems();
        shell.ShellCommands.Where(c => c.Shortcut != null).Select(c => c.Shortcut).Should().OnlyHaveUniqueItems();
        shell.ShellCommands.Select(c => c.Group).Distinct().Should().Equal("文件", "编辑", "查看", "文章", "发布", "帮助");
    }

    [Fact]
    public async Task CommandPalette_FiltersAndExecutesTheSameRegisteredCommand() {
        var shell = CreateShell();
        var palette = new CommandPaletteViewModel(shell, true) { Query = "> 新建" };
        palette.Entries.Should().ContainSingle();
        await palette.SelectedEntry!.Execute();
        shell.Workspace.Documents.Should().ContainSingle();
        palette.Query = "> 不存在的命令";
        palette.HasResults.Should().BeFalse();
    }

    [Fact]
    public async Task QuickOpen_FiltersFilesAndSwitchesWithoutDuplicatingTabs() {
        var shell = CreateShell();
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "开发规范.md");
        await File.WriteAllTextAsync(path, "# 规范");
        await shell.Workspace.OpenPathAsync(path);
        shell.Workspace.NewDocumentCommand.Execute(null);
        var palette = new CommandPaletteViewModel(shell, false) { Query = "开发规范" };
        palette.Entries.Should().ContainSingle();
        await palette.SelectedEntry!.Execute();
        shell.PublishPage.CurrentFilePath.Should().Be(path);
        shell.Workspace.Documents.Should().HaveCount(2);
    }

    [Fact]
    public void Outline_RecognizesHierarchyAndSetext_ButIgnoresCodeFences() {
        var shell = CreateShell();
        shell.Workspace.NewDocumentCommand.Execute(null);
        var document = shell.PublishPage;
        document.ArticleContent = "# Title\n\n```md\n# Not a heading\n```\n\n## Details\n\nSetext\n------\n";
        document.OutlineEntries.Select(h => h.Title).Should().Equal("Title", "Details", "Setext");
        document.OutlineEntries.Select(h => h.Line).Should().Equal(1, 7, 9);
        document.OutlineEntries.Select(h => h.Level).Should().Equal(1, 2, 2);
        var requested = 0;
        document.NavigateToLineRequested += line => requested = line;
        document.NavigateToHeadingCommand.Execute(document.OutlineEntries[1]);
        requested.Should().Be(7);
    }

    [Fact]
    public void FocusMode_RestoresLayoutAcrossDocumentSwitches() {
        var workspace = CreateShell().Workspace;
        workspace.NewDocumentCommand.Execute(null);
        var first = workspace.CurrentDocument;
        workspace.IsTaskPanelOpen = true;
        workspace.ToggleFocusCommand.Execute(null);
        workspace.IsSidebarOpen.Should().BeFalse();
        workspace.IsTaskPanelOpen.Should().BeFalse();
        first.IsInspectorOpen.Should().BeFalse();
        workspace.NewDocumentCommand.Execute(null);
        workspace.CurrentDocument.IsInspectorOpen.Should().BeFalse();
        workspace.ToggleFocusCommand.Execute(null);
        workspace.IsSidebarOpen.Should().BeTrue();
        workspace.IsTaskPanelOpen.Should().BeTrue();
        first.IsInspectorOpen.Should().BeTrue();
        workspace.CurrentDocument.IsInspectorOpen.Should().BeTrue();
    }

    [Fact]
    public void WorkspaceLayout_RestoresVisibilityAndInspectorWidth() {
        var shell = CreateShell();
        shell.Workspace.NewDocumentCommand.Execute(null);
        shell.Workspace.IsSidebarOpen = false;
        shell.Workspace.IsTaskPanelOpen = true;
        shell.PublishPage.InspectorColumnWidth = new Avalonia.Controls.GridLength(360);
        shell.PublishPage.IsInspectorOpen = false;
        var restored = CreateShell();
        restored.Workspace.NewDocumentCommand.Execute(null);
        restored.Workspace.IsSidebarOpen.Should().BeFalse();
        restored.Workspace.IsTaskPanelOpen.Should().BeTrue();
        restored.PublishPage.IsInspectorOpen.Should().BeFalse();
        restored.PublishPage.ExpandedInspectorWidth.Should().Be(360);
    }

    [Fact]
    public void EditCommands_DelegateToFocusedControlHandler() {
        var shell = CreateShell();
        string? action = null;
        shell.CanEdit = id => id == "copy";
        shell.EditRequested = id => { action = id; return Task.CompletedTask; };
        shell.GetCommand("edit.copy").Command.CanExecute(null).Should().BeTrue();
        shell.GetCommand("edit.paste").Command.CanExecute(null).Should().BeFalse();
        shell.GetCommand("edit.copy").Command.Execute(null);
        action.Should().Be("copy");
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
