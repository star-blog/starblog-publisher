using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using System.ComponentModel;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class CoverStudioView : UserControl {
    private bool? _wide;
    private CoverStudioViewModel? _studio;
    public CoverStudioView() {
        InitializeComponent();
        AddHandler(PointerPressedEvent, (_, e) => {
            if (e.Source is Avalonia.Visual visual && (visual is Slider || visual.GetVisualAncestors().OfType<Slider>().Any())
                && DataContext is CoverStudioViewModel vm && !vm.IsComposing) vm.Session.BeginGesture();
        }, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => {
            if (_studio != null) { _studio.EndInlineEdit(); _studio.CancelCompositionCommand.Execute(null); _studio.PropertyChanged -= StudioChanged; }
            _studio = DataContext as CoverStudioViewModel;
            if (_studio != null) { _studio.PropertyChanged += StudioChanged; _studio.Start(); }
            UpdateDrawer();
        };
        SizeChanged += (_, _) => {
            var wide = Bounds.Width >= 1000;
            if (_wide != wide && DataContext is CoverStudioViewModel studio) { studio.IsNarrow = !wide; studio.IsInspectorOpen = wide || _wide == null && studio.InspectorTab == 3; _wide = wide; }
            UpdateDrawer();
        };
        DetachedFromVisualTree += (_, _) => { _studio?.EndInlineEdit(); _studio?.CancelCompositionCommand.Execute(null); };
    }
    private void StudioChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(CoverStudioViewModel.IsInspectorOpen) or nameof(CoverStudioViewModel.IsNarrow)) UpdateDrawer();
        if (e.PropertyName is nameof(CoverStudioViewModel.IsTemplateLibraryOpen) or nameof(CoverStudioViewModel.IsPreviewExpanded) or nameof(CoverStudioViewModel.IsArticleImagePickerOpen))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (_studio?.IsTemplateLibraryOpen == true) TemplateSurface.Focus();
                else if (_studio?.IsArticleImagePickerOpen == true) ImagePickerSurface.Focus();
                else if (_studio?.IsPreviewExpanded == true) PreviewSurface.Focus();
                else CanvasImage.Focus();
            });
    }
    private void OnStudioKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Escape || _studio == null) return;
        if (_studio.IsTemplateLibraryOpen) _studio.CloseTemplateLibraryCommand.Execute(null);
        else if (_studio.IsArticleImagePickerOpen) _studio.CloseArticleImagesCommand.Execute(null);
        else if (_studio.IsPreviewExpanded) _studio.CloseExpandedPreviewCommand.Execute(null);
        else return;
        e.Handled = true;
    }
    private void UpdateDrawer() {
        var narrow = _studio?.IsNarrow ?? false;
        Grid.SetColumn(Inspector, narrow ? 0 : 1); Grid.SetColumnSpan(Inspector, narrow ? 2 : 1);
        DrawerScrim.IsVisible = narrow && _studio?.IsInspectorOpen == true;
    }
    private void OnDrawerDismiss(object? sender, PointerPressedEventArgs e) { if (_studio != null) _studio.IsInspectorOpen = false; e.Handled = true; }
    private void OnGestureEnd(object? sender, PointerReleasedEventArgs e) {
        if (DataContext is CoverStudioViewModel vm) vm.EndGesture();
    }
}
