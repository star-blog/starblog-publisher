using System;
using System.Windows.Input;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class SitePostEditorViewModel : IMarkdownEditorContext {
    private readonly string _previewId = Guid.NewGuid().ToString("N");
    private bool _loadingEditorPreferences;
    private double _inspectorWidth = 280;
    public TextDocument EditorDocument { get; } = new();
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public int CursorLine { get; set; } = 1;
    public int CursorColumn { get; set; } = 1;
    public string ArticleContent { get => Content; set => Content = value; }
    [ObservableProperty] private MarkdownEditorMode _editorMode;
    [ObservableProperty] private double _editorFontSize = 14;
    [ObservableProperty] private bool _editorWordWrap = true;
    [ObservableProperty] private bool _editorShowLineNumbers;
    [ObservableProperty] private Uri? _previewUri;
    public bool CanPreview => IsLoaded && !string.IsNullOrWhiteSpace(Content);
    public bool IsSourceMode { get => EditorMode == MarkdownEditorMode.Source; set { if (value) EditorMode = MarkdownEditorMode.Source; } }
    public bool IsSplitMode { get => EditorMode == MarkdownEditorMode.Split; set { if (value) EditorMode = MarkdownEditorMode.Split; } }
    public bool IsPreviewMode { get => EditorMode == MarkdownEditorMode.Preview; set { if (value) EditorMode = MarkdownEditorMode.Preview; } }
    public bool IsSourcePaneVisible => !IsPreviewMode;
    public bool IsPreviewPaneVisible => !IsSourceMode;
    public GridLength SourceColumnWidth => IsPreviewMode ? new(0) : new(1, GridUnitType.Star);
    public GridLength PreviewColumnWidth => IsSourceMode ? new(0) : new(1, GridUnitType.Star);
    public GridLength SplitterColumnWidth => new(IsSplitMode ? 4 : 0);
    public GridLength InspectorColumnWidth {
        get => new(IsSplitMode ? 0 : _inspectorWidth);
        set {
            if (!IsSplitMode && value.IsAbsolute) _inspectorWidth = Math.Clamp(value.Value, 240, 420);
            OnPropertyChanged();
        }
    }
    ICommand IMarkdownEditorContext.SetEditorModeCommand => SetEditorModeCommand;
    ICommand IMarkdownEditorContext.IncreaseEditorFontSizeCommand => IncreaseEditorFontSizeCommand;
    ICommand IMarkdownEditorContext.DecreaseEditorFontSizeCommand => DecreaseEditorFontSizeCommand;
    public event Action<int>? NavigateToLineRequested;
    public event Action<int>? NavigatePreviewToLineRequested;
    public void NavigateToLine(int line) {
        if (IsSourcePaneVisible) NavigateToLineRequested?.Invoke(line);
        if (IsPreviewPaneVisible) NavigatePreviewToLineRequested?.Invoke(line);
    }
    partial void OnEditorModeChanged(MarkdownEditorMode value) {
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(IsSplitMode));
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsSourcePaneVisible));
        OnPropertyChanged(nameof(IsPreviewPaneVisible));
        OnPropertyChanged(nameof(SourceColumnWidth));
        OnPropertyChanged(nameof(PreviewColumnWidth));
        OnPropertyChanged(nameof(SplitterColumnWidth));
        OnPropertyChanged(nameof(InspectorColumnWidth));
    }
    [RelayCommand] private void SetEditorMode(string? mode) {
        if (Enum.TryParse<MarkdownEditorMode>(mode, out var parsed) && (parsed == MarkdownEditorMode.Source || CanPreview)) EditorMode = parsed;
    }
    [RelayCommand] private void IncreaseEditorFontSize() => EditorFontSize = Math.Min(22, EditorFontSize + 1);
    [RelayCommand] private void DecreaseEditorFontSize() => EditorFontSize = Math.Max(11, EditorFontSize - 1);
    partial void OnEditorFontSizeChanged(double value) => PersistEditorPreferences();
    partial void OnEditorWordWrapChanged(bool value) => PersistEditorPreferences();
    partial void OnEditorShowLineNumbersChanged(bool value) => PersistEditorPreferences();
    private void LoadEditorPreferences() {
        _loadingEditorPreferences = true;
        var settings = AppSettings.Instance;
        EditorFontSize = settings.EditorFontSize is >= 11 and <= 22 ? settings.EditorFontSize : 14;
        EditorWordWrap = settings.EditorWordWrap;
        EditorShowLineNumbers = settings.EditorShowLineNumbers;
        _loadingEditorPreferences = false;
    }
    private void PersistEditorPreferences() {
        if (_loadingEditorPreferences) return;
        var settings = AppSettings.Instance;
        settings.EditorFontSize = EditorFontSize;
        settings.EditorWordWrap = EditorWordWrap;
        settings.EditorShowLineNumbers = EditorShowLineNumbers;
        settings.Save();
    }
    partial void OnPostUrlChanged(string? value) => RefreshPreviewForThemeChange();
    public void RefreshPreviewForThemeChange() {
        try {
            Uri.TryCreate(PostUrl, UriKind.Absolute, out var baseUri);
            PreviewUri = MarkdownPreviewService.Render(Content, _previewId, baseUri, _shell.IsDarkTheme);
        }
        catch (Exception ex) {
            PreviewUri = null;
            StatusMessage = $"预览生成失败: {ex.Message}";
        }
    }
}
