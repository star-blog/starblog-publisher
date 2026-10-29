using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class CoverStudioViewModel : ViewModelBase, IDisposable {
    private readonly IHttpClientFactory _factory;
    private readonly Action<PreparedWeChatCover> _apply;
    private readonly Func<Task<bool>>? _save;
    private readonly Func<string> _articleTitle, _summary, _content;
    private readonly SemaphoreSlim _renderGate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _renderCts, _backgroundCts, _aiCts;
    private bool _started, _disposed;
    private int _renderRevision = -1;
    private string? _backgroundCacheKey;
    private CoverRawImage? _backgroundCache;
    private CoverDocument? _interactionDocument;
    public CoverDocument LiveDocument => _interactionDocument ?? Session.Draft;
    public event Action? LiveChanged;
    public bool IsInteracting => _interactionDocument != null;
    public bool BeginInteraction() {
        if (!IsEditable || Preview == null || Preview.Data.Document != Session.Draft) return false;
        _interactionDocument = Session.Draft; return true;
    }
    public void UpdateInteraction(CoverDocument document) { if (_interactionDocument != null) { _interactionDocument = document; LiveChanged?.Invoke(); OnPropertyChanged(nameof(HeadlineWarning)); OnPropertyChanged(nameof(SecondaryWarning)); } }
    public void EndInteraction(bool commit) {
        if (_interactionDocument == null) return;
        var document = _interactionDocument; _interactionDocument = null;
        if (commit) Session.Change(document); LiveChanged?.Invoke();
    }
    public string TemplateName => Templates.FirstOrDefault(t => t.Id == Session.Draft.TemplateId)?.Name ?? "封面模板";
    public CoverTemplate? SelectedCoverTemplate { get => Templates.FirstOrDefault(t => t.Id == Session.Draft.TemplateId); set { if (value != null && value.Id != Session.Draft.TemplateId) Edit(value.Apply(Session.Draft)); } }
    public bool IsAiExpanded { get => InspectorTab == 3; set { InspectorTab = value ? 3 : 0; OnPropertyChanged(); } }
    public bool HasBackground => Session.Draft.Background != null;
    public bool IsTextTarget => DragTarget == "文字";
    public bool IsBackgroundTarget => DragTarget == "背景";
    [RelayCommand] private void SetDragTarget(string target) { EndInteraction(true); DragTarget = target; }
    partial void OnDragTargetChanged(string value) { OnPropertyChanged(nameof(IsTextTarget)); OnPropertyChanged(nameof(IsBackgroundTarget)); }
    [RelayCommand] private void ToggleGuides() => ShowGuides = !ShowGuides;
    [RelayCommand] private void ResetText() => Edit(Session.Draft with { TextX = 0, TextY = 0 });
    public CoverEditSession Session { get; }
    public CoverStudioViewModel(IHttpClientFactory httpClientFactory, string? articleTitle, Action<PreparedWeChatCover> applyToWeChat,
        IReadOnlyList<ArticleImageSource>? articleImages = null, CoverEditSession? session = null, Func<Task<bool>>? save = null,
        Func<string>? title = null, Func<string>? summary = null, Func<string>? content = null) {
        _factory = httpClientFactory; _apply = applyToWeChat; _save = save;
        _articleTitle = title ?? (() => articleTitle ?? ""); _summary = summary ?? (() => ""); _content = content ?? (() => "");
        Session = session ?? new(Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "cover-assets"), articleTitle ?? "");
        ArticleImages = new((articleImages ?? []).Select(i => new ArticleCoverImage(i)));
        Session.Changed += OnSessionChanged;
        Session.BeforeSave += CompleteEditing;
        Library = new CoverTemplateLibrary(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(AppSettings.SettingsFilePath))!, "cover-library"));
    }
    public string Title => "制作封面";
    public IReadOnlyList<CoverTemplate> Templates => CoverTemplate.All;
    public ObservableCollection<ArticleCoverImage> ArticleImages { get; }
    public ObservableCollection<RandomCoverProvider> RandomCoverProviders { get; } = new(RandomCoverCatalog.All);
    public ObservableCollection<PromptTemplate> PromptStyles { get; } = new(PromptTemplates.Cover);
    public ObservableCollection<string> TitleSuggestions { get; } = new();
    public bool HasArticleImages => ArticleImages.Count > 0;
    public string ArticleImagePickerLabel => $"文章图片（{ArticleImages.Count}）";
    public bool IsEditable => Session.IsSupported && !Session.IsBusy && !Session.IsClosed;
    public bool HasPreview => Preview != null;
    public string SaveHint => !Session.IsSupported ? "此封面版本暂不可编辑，原始数据会保留" : Session.State?.Applied == null ? "封面草稿 · 尚未应用" :
        Session.Draft == Session.State.Applied ? "当前设计已应用 · 随文章保存" : "草稿已修改 · 已应用封面保持原样";
    [ObservableProperty] private CoverPreviewLayers? _preview;
    public CoverPreviewLayers? HeadlinePreview => Preview;
    public CoverPreviewLayers? SecondaryPreview => Preview;
    [ObservableProperty] private string _statusMessage = "选择模板，调整背景和标题";
    [ObservableProperty] private bool _isArticleImagePickerOpen;
    [ObservableProperty] private bool _isFetchingRandomBackground;
    [ObservableProperty] private bool _isFetchingArticleBackground;
    [ObservableProperty] private bool _isGeneratingAi;
    [ObservableProperty] private bool _isInspectorOpen = true;
    [ObservableProperty] private int _inspectorTab;
    [ObservableProperty] private string _outputPath = "";
    [ObservableProperty] private int _effectiveFontSize;
    [ObservableProperty] private bool _showGuides;
    public bool IsAIEnabled => AppSettings.Instance.EnableAI;
    [ObservableProperty] private string _exportFormat = "JPEG";
    [ObservableProperty] private string _exportRange = "完整画布";
    [ObservableProperty] private string _dragTarget = "背景";
    public RandomCoverProvider? SelectedRandomCoverProvider { get; set; } = RandomCoverCatalog.All.FirstOrDefault();
    public PromptTemplate? SelectedPromptStyle { get; set; } = PromptTemplates.Cover.FirstOrDefault();
    public string BackgroundDescription => Session.Draft.Background == null ? "无背景图片" : Session.Draft.BackgroundName;
    public string FontSizeHint => EffectiveFontSize > 0 && EffectiveFontSize < FontSize ? $"已自动缩小至 {EffectiveFontSize}" : "文字超出时自动缩小，并检查溢出";
    private void Edit(CoverDocument d, string group = "") { if (IsComposing) CancelComposition(); Session.Change(d, group); }
    public string CoverTitle { get => Session.Draft.Title; set => Edit(Session.Draft with { Title = value }, "title"); }
    public string Subtitle { get => Session.Draft.Subtitle; set => Edit(Session.Draft with { Subtitle = value }, "subtitle"); }
    public string Column { get => Session.Draft.Column; set => Edit(Session.Draft with { Column = value }, "column"); }
    public string TextColor { get => SelectedTextStyle?.Color ?? Session.Draft.Color; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { Color = value }, "color"); else Edit(Session.Draft with { Color = value }, "color"); } }
    public string BackgroundColor { get => LiveDocument.BackgroundColor; set { if (IsComposing) UpdateInteraction(LiveDocument with { BackgroundColor = value }); else Edit(Session.Draft with { BackgroundColor = value }, "backgroundColor"); OnPropertyChanged(); OnPropertyChanged(nameof(CanvasColor)); } }
    public double FontSize { get => SelectedTextStyle?.FontSize ?? Session.Draft.FontSize; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { FontSize = (int)value }, "size"); else Edit(Session.Draft with { FontSize = (int)value }, "size"); } }
    public double TextWidth { get => SelectedTextStyle?.Width ?? Session.Draft.TextWidth; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { Width = value }, "width"); else Edit(Session.Draft with { TextWidth = value }, "width"); } }
    public double LineHeight { get => SelectedTextStyle?.LineHeight ?? Session.Draft.LineHeight; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { LineHeight = value }, "lineHeight"); else Edit(Session.Draft with { LineHeight = value }, "lineHeight"); } }
    public double Zoom { get => LiveDocument.Zoom; set { var d = LiveDocument with { Zoom = value }; if (IsComposing) UpdateInteraction(d); else Edit(d, "zoom"); OnPropertyChanged(); } }
    public double Scrim { get => LiveDocument.Scrim; set { if (IsComposing) UpdateInteraction(LiveDocument with { Scrim = value }); else Edit(Session.Draft with { Scrim = value }, "scrim"); OnPropertyChanged(); } }
    public bool IsBold { get => SelectedTextStyle?.Bold ?? Session.Draft.Bold; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { Bold = value }); else Edit(Session.Draft with { Bold = value }); } }
    public bool Centered { get => Session.Draft.Centered; set => Edit(Session.Draft with { Centered = value }); }
    public bool Manual { get => Session.Draft.Manual; set => Edit(Session.Draft with { Manual = value }); }
    public double HeadlineOffset { get => Session.Draft.Crops.HeadlineOffset; set => Edit(Session.Draft with { Crops = Session.Draft.Crops with { HeadlineOffset = value } }, "headline"); }
    public double SecondaryOffset { get => Session.Draft.Crops.SecondaryOffset; set => Edit(Session.Draft with { Crops = Session.Draft.Crops with { SecondaryOffset = value } }, "secondary"); }
    public string GeneratedPrompt { get => Session.Draft.Prompt; set => Edit(Session.Draft with { Prompt = value }, "prompt"); }
    public void Start() { if (_started) return; _started = true; SchedulePreview(); }
    private void OnSessionChanged() {
        if (_disposed) return;
        _templateCts?.Cancel();
        foreach (var name in new[] { nameof(CoverTitle), nameof(Subtitle), nameof(Column), nameof(TextColor), nameof(BackgroundColor),
            nameof(FontSize), nameof(TextWidth), nameof(LineHeight), nameof(Zoom), nameof(Scrim), nameof(IsBold), nameof(Centered), nameof(Manual),
            nameof(HeadlineOffset), nameof(SecondaryOffset), nameof(GeneratedPrompt), nameof(IsEditable), nameof(SaveHint), nameof(BackgroundDescription),
            nameof(TemplateName), nameof(HasBackground), nameof(SelectedCoverTemplate) }) OnPropertyChanged(name);
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
        NotifyEditor();
        if (_started && Session.Revision != _renderRevision) SchedulePreview();
    }
    private void SchedulePreview() {
        if (!Session.IsSupported) { StatusMessage = "此封面版本暂不可编辑，保存文章时会保留原始设计"; return; }
        _renderCts?.Cancel(); _renderCts?.Dispose(); _renderCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _renderRevision = Session.Revision;
        _ = PreviewAfterDelayAsync(_renderRevision, Session.Draft, Session.ArticlePath, _renderCts.Token);
    }
    private async Task PreviewAfterDelayAsync(int revision, CoverDocument document, string? path, CancellationToken token) {
        try { await Task.Delay(60, token); await RenderPreviewAsync(revision, document, path, token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed && revision == Session.Revision) StatusMessage = ex.Message; }
    }
    public async Task RenderNowAsync() {
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _renderRevision = Session.Revision;
        try { await RenderPreviewAsync(_renderRevision, Session.Draft, Session.ArticlePath, _renderCts.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) StatusMessage = ex.Message; }
    }
    private async Task RenderPreviewAsync(int revision, CoverDocument document, string? path, CancellationToken token) {
        await _renderGate.WaitAsync(token);
        try {
            var assetKey = document.Background == null ? null : CoverAssetService.Resolve(document.Background, path);
            var data = await Task.Run(() => {
                if (_backgroundCacheKey != assetKey) { _backgroundCacheKey = assetKey; _backgroundCache = null; }
                var layers = CoverPreviewLayerData.Create(document, path, _backgroundCache, token, _previousPreviewData);
                _backgroundCache = layers.Background;
                return layers;
            }, token);
            if (_disposed || revision != Session.Revision || token.IsCancellationRequested) return;
            var next = new CoverPreviewLayers(data, Preview);
            _previousPreviewData = data;
            var old = Preview; Preview = next; old?.Dispose();
            EffectiveFontSize = data.Layout.FontSize;
            StatusMessage = data.Layout.Diagnostics.Count == 0 ? "" : string.Join("；", data.Layout.Diagnostics.Select(d => d.Message));
            OnPropertyChanged(nameof(HeadlinePreview)); OnPropertyChanged(nameof(SecondaryPreview));
            OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(FontSizeHint));
            OnPropertyChanged(nameof(BackgroundThumbnail));
            NotifyEditor();
        } finally { _renderGate.Release(); }
    }
    [RelayCommand] private void SelectTemplate(CoverTemplate? template) { if (template != null) { CancelComposition(); Edit(CoverTemplateCatalog.ApplyLatest(template, Session.Draft)); SelectedElement = CoverElementId.None; } }
    [RelayCommand] private void SyncTitle() => CoverTitle = _articleTitle();
    [RelayCommand] private void SetTextPlacement(string value) => Edit(Session.Draft with { Placement = value, TextY = 0 });
    [RelayCommand] private void ResetBackground() => Edit(Session.Draft with { BackgroundX = 0, BackgroundY = 0, Zoom = 1 });
    [RelayCommand] private void ClearBackground() => Edit(Session.Draft with { Background = null });
    [RelayCommand] private void ClearLogo() => Edit(Session.Draft with { Logo = null });
    [RelayCommand(CanExecute = nameof(CanUndo))] private void Undo() { CancelComposition(); EndInlineEdit(); Session.Undo(); }
    [RelayCommand(CanExecute = nameof(CanRedo))] private void Redo() { CancelComposition(); EndInlineEdit(); Session.Redo(); }
    private bool CanUndo() => Session.CanUndo;
    private bool CanRedo() => Session.CanRedo;
    public void EndGesture() => Session.EndGesture();
    [RelayCommand] private void ToggleInspector() => IsInspectorOpen = !IsInspectorOpen;
    [RelayCommand] private async Task SaveArticle() { if (_save != null) StatusMessage = await _save() ? "文章与封面设计已保存" : "文章尚未保存"; }
    [RelayCommand] private async Task ApplyToWeChat() {
        EndInteraction(true);
        try {
            var prepared = await Session.ApplyAsync(_lifetime.Token);
            if (_disposed) return;
            OutputPath = prepared.Path; _apply(prepared); StatusMessage = "已应用到文章，保存文章后持久保留";
        } catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) StatusMessage = ex.Message; }
    }
    [RelayCommand] private async Task SaveImage() {
        CompleteEditing();
        await RenderNowAsync();
        if (Preview?.Data.Document != Session.Draft) return;
        if (Preview?.Data.Layout.Diagnostics.FirstOrDefault(d => d.IsError) is { } diagnostic) { SelectObject(diagnostic.Element); StatusMessage = diagnostic.Message; return; }
        var storage = GuiHost.GetTopLevel()?.StorageProvider; if (storage == null) return;
        var png = ExportFormat == "PNG";
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "导出封面图片", SuggestedFileName = "cover." + (png ? "png" : "jpg"), DefaultExtension = png ? "png" : "jpg",
            FileTypeChoices = [new FilePickerFileType(png ? "PNG" : "JPEG") { Patterns = [png ? "*.png" : "*.jpg"] }] });
        if (file == null || _disposed) return;
        var snapshot = Session.Draft; var path = Session.ArticlePath;
        var temp = Path.Combine(Session.Assets.CacheDirectory, Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg"));
        var frame = ExportRange == "头条" ? CoverPreviewFrame.Headline : ExportRange == "次条" ? CoverPreviewFrame.Secondary : CoverPreviewFrame.Full;
        if (!IsEditable) return;
        Session.SetBusy(true);
        try {
            await Task.Run(() => new CoverExportService().Export(snapshot, path, temp, png, frame, token: _lifetime.Token), _lifetime.Token);
            await using var source = File.OpenRead(temp); await using var target = await file.OpenWriteAsync();
            if (target.CanSeek) target.SetLength(0);
            await source.CopyToAsync(target, _lifetime.Token); StatusMessage = "封面图片已导出";
        } catch (OperationCanceledException) { }
        catch (Exception ex) { StatusMessage = "导出失败：" + ex.Message; }
        finally { if (File.Exists(temp)) File.Delete(temp); Session.SetBusy(false); }
    }
    [RelayCommand] private Task SelectLocalBackground() => SelectLocalAsync(false);
    [RelayCommand] private Task SelectLogo() => SelectLocalAsync(true);
    private async Task SelectLocalAsync(bool logo) {
        var storage = GuiHost.GetTopLevel()?.StorageProvider; if (storage == null) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = logo ? "选择标识图片" : "选择封面背景", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("图片") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }] });
        if (files.Count == 0 || _disposed) return;
        var token = BeginBackground();
        try { await using var stream = await files[0].OpenReadAsync(); await ImportAsync(await ReadBoundedAsync(stream, token), logo, token, files[0].Name); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusMessage = "读取图片失败：" + ex.Message; }
    }
    private CancellationToken BeginBackground() {
        _backgroundCts?.Cancel(); _backgroundCts?.Dispose(); _backgroundCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        IsFetchingRandomBackground = false; IsFetchingArticleBackground = false; return _backgroundCts.Token;
    }
    private async Task ImportAsync(byte[] bytes, bool logo, CancellationToken token, string name = "背景图片") {
        var asset = await Session.Assets.ImportAsync(bytes, token); token.ThrowIfCancellationRequested();
        if (_disposed || !IsEditable) return;
        var current = Session.Draft;
        if (!logo && current.TemplateId == "minimal") current = current.Elements == null ? current with { TemplateId = "photo" } : CoverTemplateCatalog.ApplyLatest(Templates.First(t => t.Id == "photo"), current) with { Scrim = current.Scrim };
        Edit(logo ? current with { Logo = asset } : current with { Background = asset, BackgroundName = name, BackgroundX = 0, BackgroundY = 0, Zoom = 1 });
        await RenderNowAsync();
    }
    [RelayCommand] private async Task ShowArticleImages() {
        IsArticleImagePickerOpen = true; using var gate = new SemaphoreSlim(4);
        await Task.WhenAll(ArticleImages.Select(async image => {
            if (image.Thumbnail != null) return; var entered = false;
            try {
                await gate.WaitAsync(_lifetime.Token); entered = true;
                var bytes = await ReadSourceAsync(image.Source, _lifetime.Token);
                var png = await Task.Run(() => ImagePreview.CreatePngThumbnail(bytes, 240, 160), _lifetime.Token);
                if (_disposed) return; image.Thumbnail = new Bitmap(new MemoryStream(png)); image.Status = "";
            } catch (OperationCanceledException) { }
            catch (Exception) { if (!_disposed) image.Status = "预览加载失败，点击重试"; }
            finally { if (entered) gate.Release(); }
        }));
    }
    [RelayCommand] private void CloseArticleImages() => IsArticleImagePickerOpen = false;
    [RelayCommand] private async Task SelectArticleBackground(ArticleCoverImage? image) {
        if (image == null || !ArticleImages.Contains(image) || _disposed) return;
        var token = BeginBackground(); IsFetchingArticleBackground = true; IsArticleImagePickerOpen = false;
        try { await ImportAsync(await ReadSourceAsync(image.Source, token), false, token, image.Name); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested) StatusMessage = "读取文章图片失败：" + ex.Message; }
        finally { if (!token.IsCancellationRequested) IsFetchingArticleBackground = false; }
    }
    [RelayCommand] private async Task PickRandomBackground() {
        if (SelectedRandomCoverProvider == null || _disposed) return;
        var token = BeginBackground(); IsFetchingRandomBackground = true;
        try { await ImportAsync(await ReadSourceAsync(SelectedRandomCoverProvider.CreateUri(1200, 900, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(), token), false, token, SelectedRandomCoverProvider.Name); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested) StatusMessage = "获取随机背景失败：" + ex.Message; }
        finally { if (!token.IsCancellationRequested) IsFetchingRandomBackground = false; }
    }
    private async Task<byte[]> ReadSourceAsync(string source, CancellationToken token) {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") {
            using var client = _factory.CreateClient(WeChatHttpClientRegistration.ImageDownloadClientName);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 10 * 1024 * 1024) throw new InvalidOperationException("在线图片不能超过 10 MB");
            await using var stream = await response.Content.ReadAsStreamAsync(token); return await ReadBoundedAsync(stream, token);
        }
        await using var input = File.OpenRead(source); return await ReadBoundedAsync(input, token);
    }
    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken token) {
        using var output = new MemoryStream(); var buffer = new byte[81920]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) != 0) {
            if (output.Length + read > 10 * 1024 * 1024) throw new InvalidOperationException("图片不能超过 10 MB"); output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
    [RelayCommand] private void CancelAi() => _aiCts?.Cancel();
    [RelayCommand] private async Task GeneratePrompt() => await GenerateAiAsync(false);
    [RelayCommand] private async Task SuggestTitles() => await GenerateAiAsync(true);
    private async Task GenerateAiAsync(bool titles) {
        if (!IsAIEnabled || IsGeneratingAi) return;
        _aiCts?.Dispose();
        _aiCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); var token = _aiCts.Token; IsGeneratingAi = true;
        var requestRevision = Session.Revision; var requestDesign = Session.Draft;
        try {
            var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
            if (titles) {
                var results = await ai.SuggestCoverTitlesAsync(_articleTitle(), _summary(), token); token.ThrowIfCancellationRequested();
                if (_disposed || Session.IsClosed || Session.Revision != requestRevision) { StatusMessage = "封面已变化，请重新生成标题候选"; return; }
                TitleSuggestions.Clear(); foreach (var result in results) TitleSuggestions.Add(result);
            } else {
                var prompt = await ai.GenerateCoverPromptAsync(new CoverPromptRequest(_articleTitle(), _summary(), _content(), SelectedPromptStyle?.Key,
                    "4:3", CoverTemplateCatalog.PromptContext(requestDesign)), token);
                token.ThrowIfCancellationRequested();
                if (_disposed || Session.IsClosed || Session.Revision != requestRevision) { StatusMessage = "封面已变化，请重新生成背景提示词"; return; }
                GeneratedPrompt = prompt;
            }
        } catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) StatusMessage = "AI 生成失败：" + ex.Message; }
        finally { if (!_disposed) IsGeneratingAi = false; }
    }
    [RelayCommand] private void ChooseTitle(string value) => CoverTitle = value;
    [RelayCommand] private async Task CopyPrompt() { if (GuiHost.GetTopLevel()?.Clipboard is { } clipboard) await clipboard.SetTextAsync(GeneratedPrompt); }
    public void Dispose() {
        if (_disposed) return; EndInlineEdit(); CancelComposition(); EndInteraction(false); _disposed = true; Session.Changed -= OnSessionChanged; Session.BeforeSave -= CompleteEditing;
        DisposeTemplateCards();
        _lifetime.Cancel(); _renderCts?.Cancel(); _backgroundCts?.Cancel(); _aiCts?.Cancel();
        _renderCts?.Dispose(); _backgroundCts?.Dispose(); _aiCts?.Dispose();
        var preview = Preview; Preview = null; preview?.Dispose(); _backgroundCache = null;
        foreach (var image in ArticleImages) image.Dispose();
    }
}
