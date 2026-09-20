using System;
using System.Linq;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.Search;
using StarBlogPublisher.Editor;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views.Controls;

public partial class MarkdownEditorView : UserControl {
    private PublishViewModel? _viewModel;
    private SearchPanel? _searchPanel;
    private MarkdownSyntaxColorizer? _colorizer;
    private bool _syncingText;
    private bool _editorConfigured;
    private int? _pendingPreviewLine;
    private double? _pendingPreviewSourceLine;
    private double? _sourceLineBeforeModeChange;

    public MarkdownEditorView() {
        InitializeComponent();
        PreviewBrowser.NavigationCompleted += async (_, _) => await ScrollPreviewToPendingHeading();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        ConfigureEditor();
        if (_viewModel != null) {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanging -= OnViewModelPropertyChanging;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.PropertyChanging += OnViewModelPropertyChanging;
            _viewModel.NavigateToLineRequested -= NavigateToLine;
            _viewModel.NavigatePreviewToLineRequested -= NavigatePreviewToLine;
            _viewModel.NavigateToLineRequested += NavigateToLine;
            _viewModel.NavigatePreviewToLineRequested += NavigatePreviewToLine;
        }
        ApplyEditorChrome();
        SyncTextFromViewModel();
        ScheduleRestoreEditorPosition();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel != null) {
            SaveEditorPosition();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanging -= OnViewModelPropertyChanging;
            _viewModel.NavigateToLineRequested -= NavigateToLine;
            _viewModel.NavigatePreviewToLineRequested -= NavigatePreviewToLine;
        }

        _pendingPreviewLine = null;
        _pendingPreviewSourceLine = null;
        _sourceLineBeforeModeChange = null;
        _viewModel = DataContext as PublishViewModel;
        if (_viewModel != null) {
            _syncingText = true;
            Editor.Document = _viewModel.EditorDocument;
            _syncingText = false;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.PropertyChanging += OnViewModelPropertyChanging;
            _viewModel.NavigateToLineRequested += NavigateToLine;
            _viewModel.NavigatePreviewToLineRequested += NavigatePreviewToLine;
            SyncTextFromViewModel();
            ScheduleRestoreEditorPosition();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        SaveEditorPosition();
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_viewModel != null) _viewModel.PropertyChanging -= OnViewModelPropertyChanging;
        if (_viewModel != null) _viewModel.NavigateToLineRequested -= NavigateToLine;
        if (_viewModel != null) _viewModel.NavigatePreviewToLineRequested -= NavigatePreviewToLine;
        _pendingPreviewLine = null;
        _pendingPreviewSourceLine = null;
        _sourceLineBeforeModeChange = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void SaveEditorPosition() {
        if (_viewModel == null || !_editorConfigured) return;
        _viewModel.CaretOffset = Editor.CaretOffset;
        _viewModel.VerticalOffset = Editor.VerticalOffset;
        _viewModel.HorizontalOffset = Editor.HorizontalOffset;
    }

    private void RestoreEditorPosition() {
        if (_viewModel == null || !_editorConfigured) return;
        Editor.CaretOffset = Math.Clamp(_viewModel.CaretOffset, 0, Editor.Document.TextLength);
        if (Editor.TextArea.TextView is ILogicalScrollable scrollable)
            scrollable.Offset = new Vector(_viewModel.HorizontalOffset, _viewModel.VerticalOffset);
    }

    private void ScheduleRestoreEditorPosition() {
        var document = _viewModel;
        Dispatcher.UIThread.Post(() => {
            if (ReferenceEquals(document, _viewModel)) RestoreEditorPosition();
        }, DispatcherPriority.Loaded);
    }

    private void ConfigureEditor() {
        if (_editorConfigured) {
            return;
        }

        _editorConfigured = true;
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.AllowScrollBelowDocument = true;
        Editor.TextArea.RightClickMovesCaret = true;
        Editor.Background = Brushes.Transparent;
        Editor.TextChanged += OnEditorTextChanged;
        Editor.TextArea.Caret.PositionChanged += (_, _) => {
            if (_viewModel == null) return;
            _viewModel.CursorLine = Editor.TextArea.Caret.Line;
            _viewModel.CursorColumn = Editor.TextArea.Caret.Column;
        };
        _searchPanel = SearchPanel.Install(Editor);
        ReplaceColorizer();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e) {
        if (_syncingText || _viewModel == null || _viewModel.ArticleContent == Editor.Text) {
            return;
        }

        _syncingText = true;
        _viewModel.ArticleContent = Editor.Text;
        _syncingText = false;
    }

    private void OnViewModelPropertyChanging(object? sender, System.ComponentModel.PropertyChangingEventArgs e) {
        if (e.PropertyName != nameof(PublishViewModel.EditorMode)) return;
        _sourceLineBeforeModeChange = null;
        if (_viewModel is not { IsSourcePaneVisible: true }) return;
        var view = Editor.TextArea.TextView;
        if (!view.VisualLinesValid) return;
        var firstVisible = view.VisualLines.FirstOrDefault(line => line.VisualTop + line.Height > view.VerticalOffset);
        if (firstVisible == null) return;
        var fraction = Math.Clamp((view.VerticalOffset - firstVisible.VisualTop) / firstVisible.Height, 0, 1);
        _sourceLineBeforeModeChange = firstVisible.FirstDocumentLine.LineNumber
            + fraction * (firstVisible.LastDocumentLine.LineNumber - firstVisible.FirstDocumentLine.LineNumber + 1);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PublishViewModel.EditorMode)
            && _viewModel is { IsPreviewPaneVisible: true } document
            && _sourceLineBeforeModeChange is { } sourceLine) {
            _sourceLineBeforeModeChange = null;
            _pendingPreviewLine = null;
            _pendingPreviewSourceLine = sourceLine;
            // Wait for the new preview width before calculating its block positions.
            Dispatcher.UIThread.Post(async () => {
                if (ReferenceEquals(document, _viewModel)) await ScrollPreviewToPendingHeading();
            }, DispatcherPriority.Loaded);
        }
        if (e.PropertyName == nameof(PublishViewModel.ArticleContent)) {
            _pendingPreviewLine = null;
            _pendingPreviewSourceLine = null;
            _sourceLineBeforeModeChange = null;
            SyncTextFromViewModel();
        }
    }

    private void SyncTextFromViewModel() {
        if (_syncingText || _viewModel == null || !_editorConfigured) {
            return;
        }

        var incoming = _viewModel.ArticleContent ?? string.Empty;
        if (Editor.Text == incoming) {
            return;
        }

        _syncingText = true;
        var caret = Editor.CaretOffset;
        Editor.Text = incoming;
        Editor.CaretOffset = Math.Clamp(caret, 0, incoming.Length);
        _syncingText = false;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.F || e.KeyModifiers != KeyModifiers.Control) {
            return;
        }

        if (_viewModel is { IsPreviewMode: true }) {
            _viewModel.IsSourceMode = true;
        }

        _searchPanel?.Open();
        e.Handled = true;
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) {
        ApplyEditorChrome();
        ReplaceColorizer();
    }

    private void ReplaceColorizer() {
        if (!_editorConfigured) {
            return;
        }

        var transformers = Editor.TextArea.TextView.LineTransformers;
        if (_colorizer != null) {
            transformers.Remove(_colorizer);
        }

        _colorizer = new MarkdownSyntaxColorizer(IsDarkTheme());
        transformers.Add(_colorizer);
        Editor.TextArea.TextView.Redraw();
    }

    private void ApplyEditorChrome() {
        if (!_editorConfigured) {
            return;
        }

        var isDark = IsDarkTheme();
        Editor.Foreground = BrushOrFallback(
            "TextFillColorPrimaryBrush", isDark ? Brushes.White : Brushes.Black);
        Editor.LineNumbersForeground = BrushOrFallback(
            "TextFillColorSecondaryBrush", isDark ? Brushes.LightGray : Brushes.Gray);
        Editor.TextArea.TextView.CurrentLineBackground =
            new SolidColorBrush(isDark ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0));
    }

    private async void NavigatePreviewToLine(int line) {
        _pendingPreviewSourceLine = null;
        _pendingPreviewLine = line;
        await ScrollPreviewToPendingHeading();
    }

    private async System.Threading.Tasks.Task ScrollPreviewToPendingHeading() {
        if ((_pendingPreviewLine == null && _pendingPreviewSourceLine == null)
            || _viewModel is not { IsPreviewPaneVisible: true } document) return;
        try {
            // Only scroll the current document; a tab switch may still be loading its WebView page.
            var expectedUrl = System.Text.Json.JsonSerializer.Serialize(document.PreviewUri?.AbsoluteUri);
            var sourceLine = _pendingPreviewSourceLine?.ToString(CultureInfo.InvariantCulture) ?? "null";
            await PreviewBrowser.InvokeScript($$"""
                (() => {
                    if (window.location.href !== {{expectedUrl}}) return false;
                    const sourceLine = {{sourceLine}};
                    if (sourceLine !== null) {
                        const blocks = Array.from(document.querySelectorAll('[data-source-line]'))
                            .map(element => ({ element, start: Number(element.dataset.sourceLine), end: Number(element.dataset.sourceEnd) }))
                            .filter(block => block.element.getBoundingClientRect().height > 0)
                            .sort((a, b) => a.start - b.start || b.end - a.end);
                        if (!blocks.length) return false;
                        const preceding = blocks.filter(block => block.start <= sourceLine);
                        const containing = preceding.filter(block => sourceLine < block.end + 1);
                        const block = containing.length ? containing[containing.length - 1]
                            : preceding.length ? preceding[preceding.length - 1] : blocks[0];
                        const rect = block.element.getBoundingClientRect();
                        const fraction = Math.max(0, Math.min(1, (sourceLine - block.start) / Math.max(1, block.end - block.start + 1)));
                        window.scrollTo({ top: window.scrollY + rect.top + rect.height * fraction, behavior: 'instant' });
                        return true;
                    }
                    const heading = document.querySelector('[data-outline-line="{{_pendingPreviewLine}}"]');
                    if (!heading) return false;
                    heading.scrollIntoView({ block: 'start', behavior: 'instant' });
                    return true;
                })()
                """);
        }
        catch (Exception) {
            // NavigationCompleted retries requests made while the native browser is initializing.
        }
    }

    private void NavigateToLine(int line) {
        var document = _viewModel;
        Dispatcher.UIThread.Post(() => {
            if (document != _viewModel) return;
            line = Math.Clamp(line, 1, Editor.Document.LineCount);
            Editor.CaretOffset = Editor.Document.GetLineByNumber(line).Offset;
            Editor.ScrollToLine(line);
            Editor.Focus();
        }, DispatcherPriority.Loaded);
    }

    private IBrush BrushOrFallback(string key, IBrush fallback) {
        return this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : fallback;
    }

    private bool IsDarkTheme() {
        return ActualThemeVariant == ThemeVariant.Dark;
    }
}
