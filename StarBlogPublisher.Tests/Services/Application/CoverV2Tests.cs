using System.Text.Json;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Tests.Services.Imaging;

namespace StarBlogPublisher.Tests.Services.Application;

public sealed class CoverV2Tests : IDisposable {
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cover-v2-tests", Guid.NewGuid().ToString("N"));
    private CoverAssetService Assets => new(Path.Combine(_root, "article-assets"));
    private async Task<string> Image() => await Assets.ImportAsync(SkiaFixture.EncodeSolid(600, 400, 30, 90, 200, 255, "png"));
    [Theory]
    [InlineData("minimal")][InlineData("photo")][InlineData("split")][InlineData("column")]
    public async Task NewTemplates_HaveIndependentSafeObjects_AndExportBothRatios(string id) {
        var d = CoverTemplateCatalog.ApplyLatest(CoverTemplate.All.Single(t => t.Id == id),
            new() { Title = "重新设计封面：清晰的操作与稳定的预览", Subtitle = "预览与导出使用同一份排版", Column = "设计笔记", Background = await Image() });
        using var render = new CoverRenderer().Render(d, null);
        d.TemplateVersion.Should().Be(2);
        render.Layout.Diagnostics.Should().NotContain(x => x.IsError);
        render.Layout.Objects.Select(o => o.Id).Should().Contain([CoverElementId.Title, CoverElementId.Subtitle, CoverElementId.Column]);
        foreach (var frame in new[] { CoverPreviewFrame.Headline, CoverPreviewFrame.Secondary }) {
            var crop = d.Crops.Window(1200, 900, frame == CoverPreviewFrame.Headline);
            foreach (var obj in render.Layout.Objects) {
                obj.Bounds.X.Should().BeGreaterThanOrEqualTo(crop.X);
                obj.Bounds.Right.Should().BeLessThanOrEqualTo(crop.Right);
                obj.Bounds.Y.Should().BeGreaterThanOrEqualTo(crop.Y);
                obj.Bounds.Bottom.Should().BeLessThanOrEqualTo(crop.Bottom);
            }
            var path = Path.Combine(_root, id + frame + ".png");
            var output = new CoverExportService().Export(d, null, path, png: true, frame: frame);
            output.Width.Should().Be(crop.Width); output.Height.Should().Be(crop.Height);
            output.Crops.Should().Be(d.Crops);
        }
    }
    [Theory][InlineData("photo")][InlineData("split")]
    public void ImageTemplates_RequireAnImage_AndIdentifyDiagnosticObject(string id) {
        var d = CoverTemplateCatalog.ApplyLatest(CoverTemplate.All.Single(t => t.Id == id), new());
        var layout = CoverLayoutService.Measure(d);
        layout.Diagnostics.Should().Contain(x => x.IsError && x.Element == CoverElementId.Background);
        var export = () => new CoverExportService().Export(d, null, Path.Combine(_root, "missing.jpg"));
        export.Should().Throw<InvalidOperationException>().WithMessage("*需要背景图片*");
    }
    [Fact] public async Task ColumnImageBand_IsVisible_AndLowerPanelKeepsItsColor() {
        var d = CoverTemplateCatalog.ApplyLatest(CoverTemplate.All.Single(t => t.Id == "column"), new() { Background = await Image() });
        using var render = new CoverRenderer().Render(d, null);
        render.Bitmap[500, 200].B.Should().BeGreaterThan(150);
        render.Bitmap[500, 800].R.Should().Be(0x54);
        render.Bitmap[500, 800].G.Should().Be(0x2F);
    }
    [Fact] public async Task Schema2_SourceGeneratedRoundTrip_PreservesMixedVersionsAndAppliedSnapshot() {
        var draft = CoverTemplateCatalog.Create("新版草稿") with { Background = await Image() };
        var state = new ArticleCoverState { SchemaVersion = 2, Draft = draft, Applied = new() { Title = "旧版已应用" } };
        var json = JsonSerializer.Serialize(state, CoverJsonContext.Default.ArticleCoverState);
        var restored = JsonSerializer.Deserialize(json, CoverJsonContext.Default.ArticleCoverState)!;
        restored.IsSupported.Should().BeTrue(); restored.Draft.Should().Be(draft);
        restored.Applied!.TemplateVersion.Should().Be(1); restored.Applied.Title.Should().Be("旧版已应用");
        var recovery = new CoverRecovery("v2", null, null, "", "# 正文", new() { Cover = state });
        var store = new CoverRecoveryStore(Path.Combine(_root, "recovery"));
        await store.WriteAsync(recovery, default);
        store.Read(store.Entries.Single())!.Properties.Cover!.Draft.Elements.Should().Be(draft.Elements);
    }
    [Fact] public void UnknownTemplate_RetainsRawJsonAndIsReadOnly() {
        const string json = """{"SchemaVersion":2,"Draft":{"TemplateId":"future","TemplateVersion":7,"Future":[1,2]}}""";
        var state = JsonSerializer.Deserialize(json, CoverJsonContext.Default.ArticleCoverState)!;
        state.IsSupported.Should().BeFalse();
        JsonSerializer.Serialize(state, CoverJsonContext.Default.ArticleCoverState).Should().Be(json);
    }
    [Fact] public void LegacyUpgrade_PreservesTextBoundsAndCanBeUndone() {
        var old = new CoverDocument { Title = "旧版中文封面", Subtitle = "副标题", Column = "栏目", TextX = 24, TextY = 12 };
        var before = CoverLayoutService.Measure(old);
        var session = new CoverEditSession(Path.Combine(_root, "cache"));
        session.Load(new() { Draft = old });
        session.Change(CoverObjectLayout.Upgrade(old));
        session.State!.SchemaVersion.Should().Be(2);
        var serialized = JsonSerializer.Serialize(session.State, CoverJsonContext.Default.ArticleCoverState);
        JsonSerializer.Deserialize(serialized, CoverJsonContext.Default.ArticleCoverState)!.Draft.Should().Be(session.Draft);
        var after = CoverLayoutService.Measure(session.Draft);
        foreach (var item in before.Objects) {
            var actual = after.Objects.Single(o => o.Id == item.Id);
            actual.Bounds.X.Should().BeInRange(item.Bounds.X - 1, item.Bounds.X + 1);
            actual.Bounds.Y.Should().BeInRange(item.Bounds.Y - 1, item.Bounds.Y + 1);
            actual.FontSize.Should().Be(item.FontSize);
        }
        session.Undo(); session.Draft.Should().Be(old);
    }
    [Fact] public void OverflowAndCropWarnings_IdentifyElementAndRatio() {
        var d = CoverTemplateCatalog.Create(new string('长', 3000));
        CoverLayoutService.Measure(d).Diagnostics.Should().Contain(x => x.IsError && x.Element == CoverElementId.Title);
        d = CoverTemplateCatalog.Create("顶部标题") with { Manual = true, Crops = new() { HeadlineOffset = 1 } };
        d = d with { Elements = d.Elements! with { Title = d.Elements.Title with { Y = 48 } } };
        CoverLayoutService.Measure(d).Diagnostics.Should().Contain(x => !x.IsError && x.Frame == CoverPreviewFrame.Headline && x.Element == CoverElementId.Title);
    }
    [Fact] public void FreeCrop_PreservesEffectiveTypographyInsteadOfReflowingInLargerArea() {
        var d = CoverTemplateCatalog.Create("冻结长标题的实际字号与换行，调整裁切时保持文字稳定");
        d = d with { Elements = d.Elements! with { Title = d.Elements.Title with { Width = 1000, X = -100, Y = 100 } } };
        var before = CoverLayoutService.Measure(d);
        var frozen = CoverObjectLayout.FreezePositions(d, before) with { Manual = true, Crops = new() { HeadlineOffset = .1 } };
        var after = CoverLayoutService.Measure(frozen);
        after.Lines.Select(l => l.Text).Should().Equal(before.Lines.Select(l => l.Text));
        for (var i = 0; i < before.Lines.Count; i++) {
            after.Lines[i].Size.Should().Be(before.Lines[i].Size);
            after.Lines[i].X.Should().BeApproximately(before.Lines[i].X, 1);
            after.Lines[i].Baseline.Should().BeApproximately(before.Lines[i].Baseline, 1);
        }
    }
    [Fact] public async Task Templates_OwnAssets_AndInstancesSurviveSourceAndLibraryDeletion() {
        var image = await Image();
        var source = CoverTemplateCatalog.Create("来源标题") with { Background = image, Logo = image, Column = "每周技术", Subtitle = "来源副标题" };
        var libraryRoot = Path.Combine(_root, "library");
        var library = new CoverTemplateLibrary(libraryRoot);
        var preset = await library.SaveAsync("栏目模板", true, true, source, null);
        File.Delete(image);
        var targetAssets = new CoverAssetService(Path.Combine(_root, "target-cache"));
        var instance = await library.InstantiateAsync(preset, new(), "新文章标题", targetAssets);
        instance.Title.Should().Be("新文章标题"); instance.Subtitle.Should().BeEmpty(); instance.Column.Should().Be("每周技术");
        await library.RenameAsync(preset.Id, "新名字");
        (await library.ReadAsync()).Presets.Single().Name.Should().Be("新名字");
        await library.DeleteAsync(preset.Id);
        Directory.Delete(libraryRoot, true);
        using var render = new CoverRenderer().Render(instance, null);
        render.Layout.Diagnostics.Should().NotContain(x => x.IsError);
        File.Exists(instance.Logo).Should().BeTrue(); File.Exists(instance.Background).Should().BeTrue();
    }
    [Fact] public async Task Templates_WithoutFixedBackground_KeepTargetBackground_AndRecentListIsBounded() {
        var image = await Image();
        var library = new CoverTemplateLibrary(Path.Combine(_root, "library"));
        var preset = await library.SaveAsync("普通模板", false, false, CoverTemplateCatalog.Create("旧标题"), null);
        var target = new CoverDocument { Background = image, BackgroundName = "文章配图" };
        var d = await library.InstantiateAsync(preset, target, "新文章", Assets);
        d.Background.Should().Be(image); d.BackgroundName.Should().Be("文章配图");
        for (var i = 0; i < 12; i++) await library.MarkUsedAsync(i.ToString());
        var index = await library.ReadAsync();
        index.Recent.Should().HaveCount(8); index.Recent[0].Should().Be("11");
    }
    [Fact] public async Task FailedTemplateSaveAndUnknownLibrary_DoNotOverwriteExistingData() {
        var libraryRoot = Path.Combine(_root, "library"); Directory.CreateDirectory(libraryRoot);
        var path = Path.Combine(libraryRoot, "library.json"); const string raw = """{"Version":99,"Future":"retain"}""";
        await File.WriteAllTextAsync(path, raw);
        var library = new CoverTemplateLibrary(libraryRoot);
        var save = () => library.SaveAsync("模板", false, false, CoverTemplateCatalog.Create(), null);
        await save.Should().ThrowAsync<InvalidOperationException>();
        (await File.ReadAllTextAsync(path)).Should().Be(raw);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
