using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
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

    public MarkdownEditorView() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        ConfigureEditor();
        ApplyEditorChrome();
        SyncTextFromViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel != null) {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PublishViewModel;
        if (_viewModel != null) {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncTextFromViewModel();
        }
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

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PublishViewModel.ArticleContent)) {
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

    private IBrush BrushOrFallback(string key, IBrush fallback) {
        return this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : fallback;
    }

    private bool IsDarkTheme() {
        return ActualThemeVariant == ThemeVariant.Dark;
    }
}
