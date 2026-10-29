using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

public sealed class CoverEditorInteractionTests {
    static CoverEditorInteractionTests() => Avalonia.Skia.SkiaPlatform.Initialize();
    [Fact] public void AiEntrypoint_OpensBackgroundContextWithoutChangingDesign() {
        using var vm = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "标题", _ => {}) { InspectorTab = 3 };
        vm.IsBackgroundSelection.Should().BeTrue(); vm.IsAiExpanded.Should().BeTrue(); vm.Session.State.Should().BeNull();
    }
    [Fact] public async Task SelectionIsClean_AndOnlySelectedTextStyleChanges() {
        using var vm = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "标题", _ => {});
        await vm.RenderNowAsync();
        vm.SelectObjectCommand.Execute(CoverElementId.Subtitle);
        vm.Session.State.Should().BeNull();
        var title = vm.Session.Draft.Elements!.Title;
        vm.SelectedContent = "独立副标题"; vm.FontSize = 28; vm.TextColor = "#336699";
        vm.Session.Draft.Elements.Title.Should().Be(title);
        vm.Session.Draft.Elements.Subtitle.FontSize.Should().Be(28);
        vm.Session.Draft.Subtitle.Should().Be("独立副标题");
    }
    [Fact] public async Task CropIsTransientUntilDone_SaveFlushesLatest_AndCancelDoesNotDirty() {
        using var vm = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "标题", _ => {});
        await vm.RenderNowAsync();
        await vm.AdjustHeadlineCommand.ExecuteAsync(null);
        vm.Session.State.Should().BeNull();
        vm.UpdateInteraction(vm.LiveDocument with { Manual = true, Crops = new() { HeadlineOffset = .1 } });
        vm.CancelCompositionCommand.Execute(null);
        vm.Session.State.Should().BeNull(); vm.Session.Draft.Crops.HeadlineOffset.Should().Be(.5);
        await vm.AdjustHeadlineCommand.ExecuteAsync(null);
        vm.UpdateInteraction(vm.LiveDocument with { Manual = true, Crops = new() { HeadlineOffset = .2 } });
        vm.Session.PrepareToSave();
        vm.Session.Draft.Crops.HeadlineOffset.Should().Be(.2);
        vm.UndoCommand.Execute(null); vm.Session.State.Should().BeNull();
    }
    [Fact] public async Task ThousandObjectUpdates_ReuseTexturesAndCommitOneUndoStep() {
        using var vm = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "拖动标题", _ => {});
        await vm.RenderNowAsync();
        var revision = vm.Session.Revision; var layers = vm.Preview;
        vm.BeginInteraction().Should().BeTrue();
        for (var i = 0; i < 1000; i++) vm.UpdateInteraction(vm.MoveObject(vm.Session.Draft, CoverElementId.Title, i % 50, 0));
        vm.Session.Revision.Should().Be(revision); vm.Preview.Should().BeSameAs(layers);
        vm.Session.State.Should().BeNull();
        vm.EndInteraction(true);
        vm.Session.Draft.Elements!.Title.X.Should().Be(265);
        vm.UndoCommand.Execute(null); vm.Session.State.Should().BeNull();
    }
    [Fact] public async Task InlineTextAndSidePanelShareContentAndHistory() {
        using var vm = new CoverStudioViewModel(Mock.Of<IHttpClientFactory>(), "原标题", _ => {});
        await vm.RenderNowAsync();
        vm.StartInlineEdit(CoverElementId.Title);
        vm.SelectedContent = "中"; vm.SelectedContent = "中文输入";
        vm.EndInlineEdit();
        vm.CoverTitle.Should().Be("中文输入");
        vm.UndoCommand.Execute(null); vm.CoverTitle.Should().Be("原标题");
        vm.RedoCommand.Execute(null); vm.SelectedContent.Should().Be("中文输入");
    }
    [Fact] public async Task SwitchingArticle_CancelsUnconfirmedCompositionInPreviousSession() {
        var root = Path.Combine(Path.GetTempPath(), "cover-switch-tests", Guid.NewGuid().ToString("N"));
        var shell = new MainWindowViewModel(Mock.Of<IHttpClientFactory>(), initializeSession: false, workspaceHistoryPath: Path.Combine(root, "workspace.json"));
        try {
            shell.Workspace.NewDocumentCommand.Execute(null);
            var first = shell.Workspace.CurrentDocument;
            first.ShowCoverStudioCommand.Execute(null);
            var studio = (CoverStudioViewModel)first.ActiveStackPage!;
            await studio.RenderNowAsync(); await studio.AdjustHeadlineCommand.ExecuteAsync(null);
            studio.UpdateInteraction(studio.LiveDocument with { Manual = true, Crops = new() { HeadlineOffset = 0 } });
            shell.Workspace.NewDocumentCommand.Execute(null);
            studio.IsComposing.Should().BeFalse(); first.CoverSession.State.Should().BeNull();
            studio.LiveDocument.Crops.HeadlineOffset.Should().Be(.5);
        } finally { shell.Workspace.ReleaseCoverSessions(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
