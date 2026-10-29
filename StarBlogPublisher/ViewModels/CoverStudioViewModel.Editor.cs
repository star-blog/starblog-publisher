using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public enum CoverEditorMode { Select, Text, Background, Headline, Secondary }

public partial class CoverStudioViewModel {
    private CoverPreviewLayerData? _previousPreviewData;
    private CancellationTokenSource? _templateCts;
    private bool _endingInline;
    private readonly List<CoverTemplateCard> _templateCards = [];
    public CoverTemplateLibrary Library { get; set; } = null!;
    public event Action? InlineEditRequested;
    public event Action? InlineEditEnded;
    [ObservableProperty] private CoverElementId _selectedElement;
    [ObservableProperty] private CoverEditorMode _editorMode;
    [ObservableProperty] private bool _isNarrow;
    [ObservableProperty] private bool _isTemplateLibraryOpen;
    [ObservableProperty] private string _templateGroup = "内置模板";
    [ObservableProperty] private CoverTemplateCard? _selectedTemplateCard;
    [ObservableProperty] private string _presetName = "";
    [ObservableProperty] private bool _presetIsColumn;
    [ObservableProperty] private bool _presetIncludeBackground;
    [ObservableProperty] private bool _isPresetSaveOpen;
    [ObservableProperty] private bool _isPreviewExpanded;
    [ObservableProperty] private CoverPreviewFrame _expandedFrame = CoverPreviewFrame.Headline;
    [ObservableProperty] private string _libraryMessage = "";
    public CoverDocument CropOrigin { get; private set; } = new();
    public ObservableCollection<CoverTemplateCard> VisibleTemplateCards { get; } = [];
    public ObservableCollection<CoverTemplateCard> RecentTemplateCards { get; } = [];
    public Bitmap? CandidatePreview => SelectedTemplateCard?.Thumbnail;
    public Bitmap? BackgroundThumbnail => Preview?.Background;
    public bool HasRecentTemplates => RecentTemplateCards.Count > 0;
    public bool IsBuiltInGroup => TemplateGroup == "内置模板";
    public bool IsMyTemplateGroup => TemplateGroup == "我的模板";
    public bool IsColumnTemplateGroup => TemplateGroup == "栏目预设";
    public bool HasSelectedTemplate => SelectedTemplateCard != null;
    public bool HasVisibleTemplates => VisibleTemplateCards.Count > 0;
    public bool IsPersonalTemplate => SelectedTemplateCard?.Preset != null;
    public bool IsLegacy => Session.Draft.Elements == null;
    public bool IsOverview => SelectedElement == CoverElementId.None && !IsCropping;
    public bool IsTextSelection => SelectedElement is CoverElementId.Title or CoverElementId.Subtitle or CoverElementId.Column;
    public bool IsBackgroundSelection => SelectedElement == CoverElementId.Background;
    public bool IsLogoSelection => SelectedElement == CoverElementId.Logo;
    public bool IsTitleSelection => SelectedElement == CoverElementId.Title;
    public bool CanUseBackground => Session.Draft.UsesBackgroundImage;
    public bool IsComposing => EditorMode is CoverEditorMode.Background or CoverEditorMode.Headline or CoverEditorMode.Secondary;
    public bool IsCropping => EditorMode is CoverEditorMode.Headline or CoverEditorMode.Secondary;
    public string SelectionName => IsCropping ? EditorMode == CoverEditorMode.Headline ? "头条裁切" : "次条裁切" : SelectedElement switch {
        CoverElementId.Title => "标题", CoverElementId.Subtitle => "副标题", CoverElementId.Column => "栏目名",
        CoverElementId.Logo => "标识", CoverElementId.Background => "背景", _ => "快速替换"
    };
    public string ModeHint => EditorMode switch {
        CoverEditorMode.Text => "编辑文字 · Ctrl+Enter 结束", CoverEditorMode.Background => "调整背景 · 拖动平移，滚轮缩放",
        CoverEditorMode.Headline => "调整头条裁切 · 上下拖动", CoverEditorMode.Secondary => "调整次条裁切 · 左右拖动", _ => "单击选择，双击编辑"
    };
    public string SelectedContent { get => Session.Draft.Content(IsTextSelection ? SelectedElement : CoverElementId.Title); set => Edit(Session.Draft.WithContent(IsTextSelection ? SelectedElement : CoverElementId.Title, value), "text:" + SelectedElement); }
    public CoverTextElement? SelectedTextStyle => Session.Draft.Elements?.Text(IsTextSelection ? SelectedElement : CoverElementId.Title);
    private void EditTextStyle(CoverTextElement style, string group = "") {
        if (Session.Draft.Elements is { } elements) Edit(Session.Draft with { Elements = elements.With(IsTextSelection ? SelectedElement : CoverElementId.Title, style) }, group);
    }
    public double SelectedX { get => SelectedTextStyle?.X ?? Session.Draft.TextX; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { X = value }, "position-x"); else Edit(Session.Draft with { TextX = value }); } }
    public double SelectedY { get => SelectedTextStyle?.Y ?? Session.Draft.TextY; set { if (SelectedTextStyle is { } s) EditTextStyle(s with { Y = value }, "position-y"); else Edit(Session.Draft with { TextY = value }); } }
    public CoverAlignment Alignment { get => SelectedTextStyle?.Alignment ?? (Session.Draft.Centered ? CoverAlignment.Center : CoverAlignment.Left); set { if (SelectedTextStyle is { } s) EditTextStyle(s with { Alignment = value }); else Edit(Session.Draft with { Centered = value == CoverAlignment.Center }); } }
    public IReadOnlyList<CoverAlignment> Alignments { get; } = Enum.GetValues<CoverAlignment>();
    public IReadOnlyList<CoverAlignmentChoice> AlignmentChoices { get; } = [new(CoverAlignment.Left, "左对齐"), new(CoverAlignment.Center, "居中"), new(CoverAlignment.Right, "右对齐")];
    public CoverAlignmentChoice SelectedAlignmentChoice { get => AlignmentChoices.First(a => a.Value == Alignment); set { if (value != null) Alignment = value.Value; } }
    public Color SelectedColor { get => Color.TryParse(TextColor, out var c) ? c : Colors.White; set => TextColor = value.ToString(); }
    public Color CanvasColor { get => Color.TryParse(BackgroundColor, out var c) ? c : Colors.White; set => BackgroundColor = value.ToString(); }
    public double WidthMinimum => IsLegacy ? .25 : 100;
    public double WidthMaximum => IsLegacy ? 1 : 1000;
    public double FontMinimum => SelectedElement is CoverElementId.Column or CoverElementId.Subtitle ? 20 : 24;
    public double LogoSize { get => Session.Draft.Elements?.Logo.Width ?? 64; set { if (Session.Draft.Elements is { } elements) Edit(Session.Draft with { Elements = elements with { Logo = elements.Logo with { Width = value, Height = value * .625 } } }, "logo-size"); } }
    public string HeadlineWarning => Warning(CoverPreviewFrame.Headline);
    public string SecondaryWarning => Warning(CoverPreviewFrame.Secondary);
    private string Warning(CoverPreviewFrame frame) {
        if (Preview == null) return "";
        var crop = LiveDocument.Crops.Window(1200, 900, frame == CoverPreviewFrame.Headline);
        return Preview.Data.Layout.Objects.Any(o => {
            var current = LiveDocument.Elements; var original = Preview.Data.Document.Elements;
            var dx = current == null || original == null ? 0 : o.Id == CoverElementId.Logo ? current.Logo.X - original.Logo.X : current.Text(o.Id).X - original.Text(o.Id).X;
            var dy = current == null || original == null ? 0 : o.Id == CoverElementId.Logo ? current.Logo.Y - original.Logo.Y : current.Text(o.Id).Y - original.Text(o.Id).Y;
            return o.Bounds.X + dx < crop.X || o.Bounds.Y + dy < crop.Y || o.Bounds.Right + dx > crop.Right || o.Bounds.Bottom + dy > crop.Bottom;
        }) ? "有内容被裁切" : "";
    }
    public string SelectionError => string.Join("；", Preview?.Data.Layout.Diagnostics.Where(d => d.IsError && d.Element == SelectedElement).Select(d => d.Message) ?? []);
    private void NotifyEditor() {
        foreach (var name in new[] { nameof(IsLegacy), nameof(IsOverview), nameof(IsTextSelection), nameof(IsBackgroundSelection), nameof(IsLogoSelection), nameof(IsTitleSelection), nameof(CanUseBackground), nameof(IsComposing), nameof(IsCropping), nameof(SelectionName), nameof(ModeHint), nameof(SelectedContent), nameof(SelectedX), nameof(SelectedY), nameof(Alignment), nameof(SelectedColor), nameof(CanvasColor), nameof(LogoSize), nameof(WidthMinimum), nameof(WidthMaximum), nameof(FontSize), nameof(TextColor), nameof(IsBold), nameof(TextWidth), nameof(LineHeight), nameof(HeadlineWarning), nameof(SecondaryWarning), nameof(SelectionError) }) OnPropertyChanged(name);
        LiveChanged?.Invoke();
        OnPropertyChanged(nameof(SelectedAlignmentChoice));
    }
    partial void OnSelectedElementChanged(CoverElementId value) { NotifyEditor(); OnPropertyChanged(nameof(FontMinimum)); }
    partial void OnEditorModeChanged(CoverEditorMode value) { NotifyEditor(); }
    partial void OnInspectorTabChanged(int value) {
        if (value == 3) { SelectedElement = CoverElementId.Background; IsInspectorOpen = true; }
        OnPropertyChanged(nameof(IsAiExpanded));
    }
    [RelayCommand] private void SelectObject(CoverElementId id) {
        if (IsComposing) CancelComposition(); EndInlineEdit(); EndInteraction(false); SelectedElement = id; IsInspectorOpen = true;
    }
    [RelayCommand] private void ClearSelection() { SelectObject(CoverElementId.None); }
    public void StartInlineEdit(CoverElementId id) {
        if (!IsEditable) return; SelectObject(id); EditorMode = CoverEditorMode.Text; InlineEditRequested?.Invoke();
    }
    public void EndInlineEdit() {
        if (EditorMode != CoverEditorMode.Text || _endingInline) return;
        _endingInline = true;
        try { InlineEditEnded?.Invoke(); EditorMode = CoverEditorMode.Select; Session.EndGesture(); }
        finally { _endingInline = false; }
    }
    public void CompleteEditing() { EndInlineEdit(); if (IsComposing) FinishComposition(); else EndInteraction(true); Session.EndGesture(); }
    [RelayCommand] private void UpgradeObjects() { if (IsLegacy) { Edit(CoverObjectLayout.Upgrade(Session.Draft)); SelectedElement = CoverElementId.None; } }
    [RelayCommand] private async Task AdjustBackground() => await BeginComposition(CoverEditorMode.Background);
    [RelayCommand] private async Task AdjustHeadline() => await BeginComposition(CoverEditorMode.Headline);
    [RelayCommand] private async Task AdjustSecondary() => await BeginComposition(CoverEditorMode.Secondary);
    private async Task BeginComposition(CoverEditorMode mode) {
        EndInlineEdit(); CancelComposition();
        if (!IsEditable || mode == CoverEditorMode.Background && !HasBackground) return;
        if (IsLegacy && mode != CoverEditorMode.Background) { StatusMessage = "请先升级为独立对象，再调整发布裁切"; IsInspectorOpen = true; return; }
        if (Preview?.Data.Document != Session.Draft) await RenderNowAsync();
        if (!BeginInteraction()) return;
        CropOrigin = CoverObjectLayout.FreezePositions(Session.Draft, Preview!.Data.Layout);
        EditorMode = mode; SelectedElement = mode == CoverEditorMode.Background ? CoverElementId.Background : CoverElementId.None;
    }
    [RelayCommand] private void ExpandPreview(CoverPreviewFrame frame) { ExpandedFrame = frame; IsPreviewExpanded = true; }
    [RelayCommand] private void CloseExpandedPreview() => IsPreviewExpanded = false;
    [RelayCommand] private void FinishComposition() { if (!IsComposing) return; EndInteraction(true); EditorMode = CoverEditorMode.Select; Session.EndGesture(); }
    [RelayCommand] private void CancelComposition() { EndInteraction(false); if (IsComposing) EditorMode = CoverEditorMode.Select; Session.EndGesture(); }
    [RelayCommand] private void ResetComposition() {
        if (!IsComposing) return;
        UpdateInteraction(EditorMode == CoverEditorMode.Background ? LiveDocument with { Zoom = 1, BackgroundX = 0, BackgroundY = 0 } :
            LiveDocument with { Crops = LiveDocument.Crops with { HeadlineOffset = EditorMode == CoverEditorMode.Headline ? .5 : LiveDocument.Crops.HeadlineOffset,
                SecondaryOffset = EditorMode == CoverEditorMode.Secondary ? .5 : LiveDocument.Crops.SecondaryOffset } });
        OnPropertyChanged(nameof(Zoom));
    }
    public CoverDocument MoveObject(CoverDocument origin, CoverElementId id, double dx, double dy) {
        if (origin.Elements is not { } elements) return origin with { TextX = origin.TextX + dx, TextY = origin.TextY + dy };
        var area = CoverObjectLayout.Safe(origin);
        var bounds = Preview?.Data.Layout.Objects.FirstOrDefault(o => o.Id == id)?.Bounds ?? new CoverPixelRect();
        dx = Math.Clamp(dx, area.X - bounds.X, Math.Max(area.X - bounds.X, area.Right - bounds.Right));
        dy = Math.Clamp(dy, area.Y - bounds.Y, Math.Max(area.Y - bounds.Y, area.Bottom - bounds.Bottom));
        if (id == CoverElementId.Logo) return origin with { Elements = elements with { Logo = elements.Logo with { X = elements.Logo.X + dx, Y = elements.Logo.Y + dy } } };
        var text = elements.Text(id); return origin with { Elements = elements.With(id, text with { X = text.X + dx, Y = text.Y + dy }) };
    }
    public void Nudge(double dx, double dy) {
        if (!IsEditable || EditorMode != CoverEditorMode.Select || !IsTextSelection && !IsLogoSelection) return;
        Edit(MoveObject(Session.Draft, SelectedElement, dx, dy), "nudge:" + SelectedElement);
    }
    partial void OnTemplateGroupChanged(string value) {
        FilterTemplateCards(); OnPropertyChanged(nameof(IsBuiltInGroup)); OnPropertyChanged(nameof(IsMyTemplateGroup)); OnPropertyChanged(nameof(IsColumnTemplateGroup));
    }
    partial void OnSelectedTemplateCardChanged(CoverTemplateCard? value) {
        foreach (var card in _templateCards) card.IsSelected = ReferenceEquals(card, value);
        if (value != null) PresetName = value.Name;
        OnPropertyChanged(nameof(CandidatePreview)); OnPropertyChanged(nameof(IsPersonalTemplate));
        OnPropertyChanged(nameof(HasSelectedTemplate));
    }
    private void FilterTemplateCards() {
        VisibleTemplateCards.Clear();
        foreach (var card in _templateCards.Where(c => TemplateGroup == "内置模板" ? c.BuiltIn != null : TemplateGroup == "栏目预设" ? c.Preset?.IsColumn == true : c.Preset?.IsColumn == false)) VisibleTemplateCards.Add(card);
        SelectedTemplateCard = VisibleTemplateCards.FirstOrDefault(c => c.IsSelected) ?? VisibleTemplateCards.FirstOrDefault();
        OnPropertyChanged(nameof(HasVisibleTemplates));
    }
    [RelayCommand] private void SetTemplateGroup(string value) => TemplateGroup = value;
    [RelayCommand] private void ChooseTemplateCard(CoverTemplateCard card) { TemplateGroup = card.BuiltIn != null ? "内置模板" : card.Preset!.IsColumn ? "栏目预设" : "我的模板"; SelectedTemplateCard = card; }
    [RelayCommand] private void CloseTemplateLibrary() { IsTemplateLibraryOpen = false; _templateCts?.Cancel(); }
    [RelayCommand] private async Task OpenTemplateLibrary() {
        EndInlineEdit(); CancelComposition(); IsTemplateLibraryOpen = true;
        await LoadTemplateCards();
    }
    private async Task LoadTemplateCards() {
        _templateCts?.Cancel(); _templateCts?.Dispose(); _templateCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _templateCts.Token;
        LibraryMessage = "";
        DisposeTemplateCards();
        CoverLibraryIndex index = new();
        try { index = await Library.ReadAsync(token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { LibraryMessage = StatusMessage = "模板库读取失败：" + ex.Message; }
        foreach (var template in Templates) _templateCards.Add(new("builtin:" + template.Id, template.Name, CoverTemplateCatalog.Description(template.Id), template));
        foreach (var preset in index.Presets) _templateCards.Add(new(preset.Id, preset.Name, preset.IsColumn ? "栏目预设" : "个人模板", preset: preset));
        FilterTemplateCards(); RecentTemplateCards.Clear();
        foreach (var id in index.Recent) if (_templateCards.FirstOrDefault(c => c.Id == id) is { } card) RecentTemplateCards.Add(card);
        OnPropertyChanged(nameof(HasRecentTemplates));
        SelectedTemplateCard = VisibleTemplateCards.FirstOrDefault(c => c.BuiltIn?.Id == Session.Draft.TemplateId) ?? VisibleTemplateCards.FirstOrDefault();
        var snapshot = Session.Draft; var articlePath = Session.ArticlePath;
        try {
            foreach (var card in _templateCards.ToArray()) {
                token.ThrowIfCancellationRequested();
                var d = card.BuiltIn != null ? CoverTemplateCatalog.ApplyLatest(card.BuiltIn, snapshot) : card.Preset!.Design with {
                    Title = snapshot.Title, Subtitle = snapshot.Subtitle,
                    Background = card.Preset.IncludeBackground && card.Preset.Design.Background != null ? CoverAssetService.Resolve(card.Preset.Design.Background, Path.Combine(Library.DirectoryPath, "library.json")) : snapshot.Background == null ? null : CoverAssetService.Resolve(snapshot.Background, articlePath),
                    Logo = card.Preset.Design.Logo == null ? null : CoverAssetService.Resolve(card.Preset.Design.Logo, Path.Combine(Library.DirectoryPath, "library.json"))
                };
                await _renderGate.WaitAsync(token);
                byte[] png;
                try {
                    png = await Task.Run(() => {
                        using var rendered = new CoverRenderer().Render(d, articlePath, token);
                        return ImagePreview.CreatePngThumbnail(rendered.Bitmap.PreviewPng(), 320, 240);
                    }, token);
                } finally { _renderGate.Release(); }
                token.ThrowIfCancellationRequested(); card.Thumbnail = new Bitmap(new MemoryStream(png));
                if (ReferenceEquals(card, SelectedTemplateCard)) OnPropertyChanged(nameof(CandidatePreview));
            }
        } catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested) StatusMessage = "模板预览失败：" + ex.Message; }
    }
    [RelayCommand] private async Task UseTemplate() {
        if (SelectedTemplateCard is not { } card || !IsEditable) return;
        var revision = Session.Revision;
        try {
            var d = card.BuiltIn != null ? CoverTemplateCatalog.ApplyLatest(card.BuiltIn, Session.Draft) :
                await Library.InstantiateAsync(card.Preset!, Session.Draft, _articleTitle(), Session.Assets, _lifetime.Token);
            if (_disposed || Session.IsClosed || revision != Session.Revision) { StatusMessage = "设计已变化，请重新使用模板"; return; }
            CloseTemplateLibrary(); Edit(d); SelectedElement = CoverElementId.None;
            await Library.MarkUsedAsync(card.Id, _lifetime.Token);
        } catch (OperationCanceledException) { }
        catch (Exception ex) { StatusMessage = "使用模板失败：" + ex.Message; }
    }
    [RelayCommand] private void ShowPresetSave() { IsPresetSaveOpen = !IsPresetSaveOpen; PresetName = Session.Draft.Column.Length > 0 ? Session.Draft.Column : TemplateName; }
    [RelayCommand] private async Task SavePreset() => await SavePresetCore(false);
    [RelayCommand] private async Task UpdatePreset() => await SavePresetCore(true);
    private async Task SavePresetCore(bool update) {
        if (update && SelectedTemplateCard?.Preset == null) return;
        try {
            var saved = await Library.SaveAsync(PresetName, update ? SelectedTemplateCard?.Preset?.IsColumn ?? PresetIsColumn : PresetIsColumn,
                update ? SelectedTemplateCard?.Preset?.IncludeBackground ?? PresetIncludeBackground : PresetIncludeBackground,
                Session.Draft, Session.ArticlePath, update ? SelectedTemplateCard?.Preset?.Id : null, _lifetime.Token);
            IsPresetSaveOpen = false; await LoadTemplateCards(); TemplateGroup = saved.IsColumn ? "栏目预设" : "我的模板";
            SelectedTemplateCard = _templateCards.FirstOrDefault(c => c.Id == saved.Id); LibraryMessage = StatusMessage = "模板已保存到本机";
        } catch (OperationCanceledException) { }
        catch (Exception ex) { LibraryMessage = StatusMessage = "保存模板失败：" + ex.Message; }
    }
    [RelayCommand] private async Task RenamePreset() {
        if (SelectedTemplateCard?.Preset is not { } preset) return;
        try { await Library.RenameAsync(preset.Id, PresetName, _lifetime.Token); await LoadTemplateCards(); }
        catch (Exception ex) { StatusMessage = "重命名失败：" + ex.Message; }
    }
    [RelayCommand] private async Task DeletePreset() {
        if (SelectedTemplateCard?.Preset is not { } preset) return;
        try { await Library.DeleteAsync(preset.Id, _lifetime.Token); await LoadTemplateCards(); }
        catch (Exception ex) { StatusMessage = "删除模板失败：" + ex.Message; }
    }
    private void DisposeTemplateCards() { SelectedTemplateCard = null; VisibleTemplateCards.Clear(); RecentTemplateCards.Clear(); foreach (var card in _templateCards) card.Dispose(); _templateCards.Clear(); }
}
