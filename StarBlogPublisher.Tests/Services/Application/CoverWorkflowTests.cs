using System.Text.Json;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Tests.Services.Imaging;

namespace StarBlogPublisher.Tests.Services.Application;

public class CoverWorkflowTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cover-workflow", Guid.NewGuid().ToString("N"));
    public CoverWorkflowTests() => Directory.CreateDirectory(_directory);
    [Theory]
    [InlineData("minimal")][InlineData("photo")][InlineData("split")][InlineData("column")]
    public void Templates_RenderBothCropsAndRetainText(string id) {
        var d = CoverTemplate.All.Single(t => t.Id == id).Apply(new() { Title = "中文封面：布局验证", Subtitle = "副标题", Column = "技术笔记" });
        using var result = new CoverRenderer().Render(d, null);
        result.Layout.Diagnostics.Should().BeEmpty();
        result.Layout.Lines.Should().Contain(l => l.Text.Contains("中文"));
        result.Bitmap.PreviewPng(d.Crops.Window(1200, 900, true)).Should().NotBeEmpty();
        result.Bitmap.PreviewPng(d.Crops.Window(1200, 900, false)).Should().NotBeEmpty();
    }
    [Fact] public void ExcessiveText_HasExplicitErrorAndCannotBeExported() {
        var d = new CoverDocument { Title = new string('长', 3000) };
        CoverLayoutService.Measure(d).Diagnostics.Should().Contain(d => d.IsError);
        var act = () => new CoverExportService().Export(d, null, Path.Combine(_directory, "bad.jpg"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*文字放不下*");
        File.Exists(Path.Combine(_directory, "bad.jpg")).Should().BeFalse();
    }
    [Fact] public async Task DraftAppliedAndHistory_AreIndependentAndApplyCapturesLatest() {
        var session = new CoverEditSession(_directory, "原标题");
        session.State.Should().BeNull();
        session.Change(session.Draft with { Title = "第一个字" }, "typing");
        session.Change(session.Draft with { Title = "最终标题" }, "typing");
        session.Undo(); session.State.Should().BeNull();
        session.Redo(); session.Draft.Title.Should().Be("最终标题");
        var cover = await session.ApplyAsync();
        File.Exists(cover.Path).Should().BeTrue();
        session.Change(session.Draft with { Title = "新的草稿" });
        session.State!.Applied!.Title.Should().Be("最终标题");
        session.Draft.Title.Should().Be("新的草稿");
        session.Undo(); session.Undo();
        session.State!.Applied!.Title.Should().Be("最终标题");
        session.Redo(); session.Redo();
        session.Draft.Title.Should().Be("新的草稿");
        session.State!.Applied!.Title.Should().Be("最终标题");
        new FileInfo(cover.Path).Length.Should().BeLessThanOrEqualTo(2 * 1024 * 1024);
    }
    [Fact] public async Task AssetsAndDesign_SurviveSaveAsAndMovingArticleDirectory() {
        var session = new CoverEditSession(Path.Combine(_directory, "cache"));
        var bytes = SkiaFixture.EncodeSolid(120, 90, 200, 20, 30, 255, "png");
        var asset = await session.Assets.ImportAsync(bytes);
        (await session.Assets.ImportAsync(bytes)).Should().Be(asset);
        session.Change(new() { Title = "便携封面", Background = asset, TemplateId = "photo" });
        await session.ApplyAsync();
        var path = Path.Combine(_directory, "original", "article.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var portable = await session.Assets.PersistAsync(session.State, null, path);
        Path.IsPathRooted(portable!.Draft.Background!).Should().BeFalse();
        await ArticleSidecar.WriteAsync(path, new() { Title = "文章", Cover = portable });
        var moved = Path.Combine(_directory, "moved"); Directory.Move(Path.GetDirectoryName(path)!, moved);
        var movedPath = Path.Combine(moved, "article.md");
        var restored = await ArticleSidecar.ReadAsync(movedPath);
        using var render = new CoverRenderer().Render(restored!.Cover!.Applied!, movedPath);
        render.Bitmap.Width.Should().Be(1200);
        var second = Path.Combine(_directory, "copy", "copy.md");
        var copied = await session.Assets.PersistAsync(restored.Cover, movedPath, second);
        File.Exists(CoverAssetService.Resolve(copied!.Draft.Background!, second)).Should().BeTrue();
    }
    [Fact] public async Task FutureSchema_RoundTripsRawDataWithoutReadingItsDesign() {
        var path = Path.Combine(_directory, "future.md");
        var raw = """{"Title":"未来文章","Cover":{"SchemaVersion":42,"Draft":"future-format","NewProperty":[1,2]}}""";
        await File.WriteAllTextAsync(ArticleSidecar.PathFor(path), raw);
        var sidecar = await ArticleSidecar.ReadAsync(path);
        sidecar!.Cover!.IsSupported.Should().BeFalse(); sidecar.Title = "修改标题";
        await ArticleSidecar.WriteAsync(path, sidecar);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(ArticleSidecar.PathFor(path)));
        json.RootElement.GetProperty("Cover").GetProperty("Draft").GetString().Should().Be("future-format");
        json.RootElement.GetProperty("Cover").GetProperty("NewProperty").GetArrayLength().Should().Be(2);
    }
    [Fact] public void ManualCrop_UsesIdenticalPixelWindowsForWireAndPreview() {
        var crops = new CoverCropSet { HeadlineOffset = 0, SecondaryOffset = 1 };
        var head = crops.Window(1200, 900, true); head.Y.Should().Be(0);
        var square = crops.Window(1200, 900, false); square.X.Should().Be(300);
        var wire = WeChatCoverCrops.From(crops, 1200, 900);
        wire[0].Y1.Should().Be("0.0000"); wire[1].X1.Should().Be("0.2500");
        (crops with { HeadlineOffset = double.NaN }).IsValid.Should().BeFalse();
    }
    [Fact] public async Task Recovery_RoundTripsSnapshotsAndClearDoesNotAffectArticle() {
        var store = new CoverRecoveryStore(Path.Combine(_directory, "recovery"));
        var recovery = new CoverRecovery("test", null, null, "", "# 未保存正文", new() { Title = "未保存", Cover = new() { Draft = new() { Title = "草稿" } } });
        await store.WriteAsync(recovery, default);
        var restored = store.Read(store.Entries.Single());
        restored!.Markdown.Should().Be(recovery.Markdown); restored.Properties.Cover!.Draft.Title.Should().Be("草稿");
        store.Delete("test"); store.Entries.Should().BeEmpty();
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
