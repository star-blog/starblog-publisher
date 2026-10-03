using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Search;
using StarBlogPublisher.Editor;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class SitePostEditorView : UserControl {
    private MarkdownSyntaxColorizer? _colorizer;
    private SitePostEditorViewModel? _viewModel;
    private bool _syncingText;

    public SitePostEditorView() {
        InitializeComponent();
        OnlineEditor.Options.HighlightCurrentLine = true;
        OnlineEditor.Options.EnableHyperlinks = false;
        OnlineEditor.Options.EnableEmailHyperlinks = false;
        OnlineEditor.Options.AllowScrollBelowDocument = true;
        OnlineEditor.TextArea.RightClickMovesCaret = true;
        OnlineEditor.Background = Brushes.Transparent;
        SearchPanel.Install(OnlineEditor);
        DataContextChanged += (_, _) => AttachViewModel();
        OnlineEditor.TextChanged += (_, _) => {
            if (!_syncingText && _viewModel != null) _viewModel.Content = OnlineEditor.Text;
        };
        ActualThemeVariantChanged += (_, _) => ApplyEditorTheme();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        AttachViewModel();
        ApplyEditorTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachViewModel() {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as SitePostEditorViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelChanged;
        SyncContent();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(SitePostEditorViewModel.Content)) SyncContent();
    }

    private void SyncContent() {
        var content = _viewModel?.Content ?? string.Empty;
        if (OnlineEditor.Text == content) return;
        _syncingText = true;
        OnlineEditor.Text = content;
        _syncingText = false;
    }

    private void ApplyEditorTheme() {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        OnlineEditor.Foreground = ResourceBrush("TextFillColorPrimaryBrush", dark ? Brushes.White : Brushes.Black);
        OnlineEditor.LineNumbersForeground = ResourceBrush("TextFillColorSecondaryBrush", Brushes.Gray);
        OnlineEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
            dark ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0));
        var transformers = OnlineEditor.TextArea.TextView.LineTransformers;
        if (_colorizer != null) transformers.Remove(_colorizer);
        _colorizer = new MarkdownSyntaxColorizer(dark);
        transformers.Add(_colorizer);
        OnlineEditor.TextArea.TextView.Redraw();
    }

    private IBrush ResourceBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;
}
