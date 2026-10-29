using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class CoverStudioView : UserControl {
    private Point? _last;
    private bool? _wide;
    public CoverStudioView() {
        InitializeComponent();
        AddHandler(PointerPressedEvent, (_, e) => {
            if (e.Source is Visual visual && (visual is Slider || visual.GetVisualAncestors().OfType<Slider>().Any())
                && DataContext is CoverStudioViewModel vm) vm.Session.BeginGesture();
        }, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => {
            if (DataContext is CoverStudioViewModel studio) {
                studio.Start();
                studio.PropertyChanged += (_, _) => Guides.InvalidateVisual();
            }
        };
        SizeChanged += (_, _) => {
            var wide = Bounds.Width >= 1000;
            if (_wide != wide && DataContext is CoverStudioViewModel studio) { studio.IsInspectorOpen = wide; _wide = wide; }
        };
    }
    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e) {
        if (sender is not Image image || !e.GetCurrentPoint(image).Properties.IsLeftButtonPressed) return;
        if (DataContext is CoverStudioViewModel vm) vm.Session.BeginGesture();
        _last = e.GetPosition(image); e.Pointer.Capture(image); e.Handled = true;
    }
    private void OnCanvasMoved(object? sender, PointerEventArgs e) {
        if (_last is not { } last || sender is not Image image || DataContext is not CoverStudioViewModel vm) return;
        var point = e.GetPosition(image); var scale = System.Math.Min(image.Bounds.Width / 1200, image.Bounds.Height / 900);
        if (scale > 0) vm.Drag((point.X - last.X) / scale, (point.Y - last.Y) / scale);
        _last = point;
    }
    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e) { _last = null; e.Pointer.Capture(null); OnGestureEnd(sender, e); }
    private void OnGestureEnd(object? sender, PointerReleasedEventArgs e) { if (DataContext is CoverStudioViewModel vm) vm.EndGesture(); }
    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e) {
        if (DataContext is CoverStudioViewModel vm) { vm.Zoom = System.Math.Clamp(vm.Zoom + e.Delta.Y * .1, 1, 4); e.Handled = true; }
    }
}
