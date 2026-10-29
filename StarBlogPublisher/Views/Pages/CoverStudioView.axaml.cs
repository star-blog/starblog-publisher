using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class CoverStudioView : UserControl {
    private bool? _wide;
    public CoverStudioView() {
        InitializeComponent();
        AddHandler(PointerPressedEvent, (_, e) => {
            if (e.Source is Avalonia.Visual visual && (visual is Slider || visual.GetVisualAncestors().OfType<Slider>().Any())
                && DataContext is CoverStudioViewModel vm) vm.Session.BeginGesture();
        }, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => { if (DataContext is CoverStudioViewModel studio) studio.Start(); };
        SizeChanged += (_, _) => {
            var wide = Bounds.Width >= 900;
            if (_wide != wide && DataContext is CoverStudioViewModel studio) { studio.IsInspectorOpen = wide; _wide = wide; }
        };
    }
    private void OnGestureEnd(object? sender, PointerReleasedEventArgs e) {
        if (DataContext is CoverStudioViewModel vm) vm.EndGesture();
    }
}