using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views.Controls;

/// <summary>Composites cached RGBA layers. Pointer movement only changes transforms, never the article or renderer.</summary>
public sealed class CoverCanvas : Control {
    public static readonly StyledProperty<CoverPreviewFrame> FrameProperty = AvaloniaProperty.Register<CoverCanvas, CoverPreviewFrame>(nameof(Frame));
    public CoverPreviewFrame Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    private CoverStudioViewModel? _vm;
    private Point? _start;
    private CoverDocument? _origin;
    private bool _text;
    private IPointer? _pointer;
    public int InteractionFrames { get; private set; }
    public CoverCanvas() {
        Focusable = true; ClipToBounds = true; Cursor = new Cursor(StandardCursorType.SizeAll);
        DataContextChanged += (_, _) => Connect(DataContext as CoverStudioViewModel);
        AttachedToVisualTree += (_, _) => Connect(DataContext as CoverStudioViewModel);
        DetachedFromVisualTree += (_, _) => Connect(null);
        PointerPressed += Press; PointerMoved += Move; PointerReleased += Release;
        PointerCaptureLost += (_, _) => Finish(false);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Finish(false); e.Handled = true; } };
        PointerWheelChanged += (_, e) => {
            if (Frame != CoverPreviewFrame.Full || _vm == null || !_vm.IsEditable || _start != null) return;
            _vm.Zoom = Math.Clamp(_vm.Zoom + e.Delta.Y * .1, 1, 4); e.Handled = true;
        };
    }
    private void Connect(CoverStudioViewModel? vm) {
        if (_vm == vm) return;
        Finish(false);
        if (_vm != null) { _vm.PropertyChanged -= Changed; _vm.LiveChanged -= Refresh; }
        _vm = vm;
        if (vm != null) { vm.PropertyChanged += Changed; vm.LiveChanged += Refresh; }
        InvalidateVisual();
    }
    private void Refresh() => InvalidateVisual();
    private void Changed(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(CoverStudioViewModel.Preview) or nameof(CoverStudioViewModel.ShowGuides) or nameof(CoverStudioViewModel.DragTarget)
            or nameof(CoverStudioViewModel.Zoom) or nameof(CoverStudioViewModel.Scrim) or nameof(CoverStudioViewModel.BackgroundColor)
            or nameof(CoverStudioViewModel.HeadlineOffset) or nameof(CoverStudioViewModel.SecondaryOffset)) InvalidateVisual();
    }
    private (double Scale, double X, double Y, CoverPixelRect Crop) Viewport() {
        var d = _vm!.LiveDocument;
        var crop = Frame == CoverPreviewFrame.Full ? new CoverPixelRect(0, 0, 1200, 900) : d.Crops.Window(1200, 900, Frame == CoverPreviewFrame.Headline);
        var scale = Math.Min(Bounds.Width / crop.Width, Bounds.Height / crop.Height);
        return (scale, (Bounds.Width - crop.Width * scale) / 2 - crop.X * scale,
            (Bounds.Height - crop.Height * scale) / 2 - crop.Y * scale, crop);
    }
    public override void Render(DrawingContext context) {
        if (_vm?.Preview is not { } layers) return;
        var d = _vm.LiveDocument;
        var v = Viewport(); if (v.Scale <= 0) return;
        Rect Map(double x, double y, double w, double h) => new(v.X + x * v.Scale, v.Y + y * v.Scale, w * v.Scale, h * v.Scale);
        var cropBounds = Map(v.Crop.X, v.Crop.Y, v.Crop.Width, v.Crop.Height);
        using (context.PushClip(cropBounds)) {
            var color = Color.TryParse(d.BackgroundColor, out var parsed) ? parsed : Color.Parse(layers.Data.Document.BackgroundColor);
            context.FillRectangle(new SolidColorBrush(color), Map(0, 0, 1200, 900));
            if (layers.Background is { } bg && d.UsesBackgroundImage) {
                var target = d.TemplateId == "split" ? new Rect(696, 0, 504, 900) : new Rect(0, 0, 1200, 900);
                var scale = Math.Max(target.Width / bg.PixelSize.Width, target.Height / bg.PixelSize.Height) * d.Zoom;
                var w = bg.PixelSize.Width * scale; var h = bg.PixelSize.Height * scale;
                var x = Math.Clamp(target.X + (target.Width - w) / 2 + d.BackgroundX, target.Right - w, target.X);
                var y = Math.Clamp(target.Y + (target.Height - h) / 2 + d.BackgroundY, target.Bottom - h, target.Y);
                using (context.PushClip(Map(target.X, target.Y, target.Width, target.Height)))
                    context.DrawImage(bg, new Rect(0, 0, bg.Size.Width, bg.Size.Height), Map(x, y, w, h));
            }
            if (d.Scrim > 0) {
                var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative) };
                brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Math.Clamp(d.Scrim, 0, 1) * 255), 0, 0, 0), 1));
                context.FillRectangle(brush, Map(0, 0, 1200, 900));
            }
            context.DrawImage(layers.Decoration, Map(0, 0, 1200, 900));
            var dx = d.TextX - (_vm.IsInteracting ? layers.Data.Layout.EffectiveTextX : layers.Data.Document.TextX);
            var dy = d.TextY - (_vm.IsInteracting ? layers.Data.Layout.EffectiveTextY : layers.Data.Document.TextY);
            context.DrawImage(layers.Text, Map(dx, dy, 1200, 900));
            if (Frame == CoverPreviewFrame.Full && _vm.ShowGuides) {
                var pen = new Pen(new SolidColorBrush(Colors.White, .45), 1, new DashStyle([4, 4], 0));
                var safe = CoverSafeZone.Inset(CoverSafeZone.Intersect(d.Crops.Window(1200, 900, true), d.Crops.Window(1200, 900, false)), 48);
                foreach (var r in new[] { d.Crops.Window(1200, 900, true), d.Crops.Window(1200, 900, false), safe })
                    context.DrawRectangle(null, pen, Map(r.X, r.Y, r.Width, r.Height));
            }
            if (Frame == CoverPreviewFrame.Full && _vm.IsTextTarget && layers.Data.TextBounds is { Width: > 0 } bounds) {
                var selection = Map(bounds.X + dx - 10, bounds.Y + dy - 8, bounds.Width + 20, bounds.Height + 16);
                context.DrawRectangle(null, new Pen((IBrush?)this.FindResource("AccentFillColorDefaultBrush") ?? Brushes.CornflowerBlue, 1), selection, 3, 3);
            }
        }
    }
    private Point Logical(Point point) { var v = Viewport(); return new((point.X - v.X) / v.Scale, (point.Y - v.Y) / v.Scale); }
    private void Press(object? sender, PointerPressedEventArgs e) {
        if (Frame != CoverPreviewFrame.Full || _vm?.Preview == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = Logical(e.GetPosition(this)); if (p.X is < 0 or > 1200 || p.Y is < 0 or > 900) return;
        var data = _vm.Preview.Data; var r = data.TextBounds;
        _text = r.Width > 0 && (_vm.IsTextTarget || new Rect(r.X - 10, r.Y - 8, r.Width + 20, r.Height + 16).Contains(p))
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        _vm.DragTarget = _text ? "文字" : "背景";
        if (!_vm.BeginInteraction()) return;
        _origin = _vm.Session.Draft with { TextX = data.Layout.EffectiveTextX, TextY = data.Layout.EffectiveTextY };
        _start = p; _pointer = e.Pointer; InteractionFrames = 0; Focus(); e.Pointer.Capture(this); e.Handled = true;
    }
    private void Move(object? sender, PointerEventArgs e) {
        if (_vm == null || _start is not { } start || _origin is not { } d || _vm.Preview is not { } layers) return;
        var p = Logical(e.GetPosition(this)); var delta = p - start;
        if (_text) {
            var area = CoverSafeZone.Inset(d.Manual ? new(0, 0, 1200, 900) : CoverSafeZone.Intersect(d.Crops.Window(1200, 900, true), d.Crops.Window(1200, 900, false)), 48);
            var r = layers.Data.TextBounds;
            var height = layers.Data.Layout.Lines.Sum(l => l.Size * d.LineHeight * 1.2);
            var dx = Math.Clamp(delta.X, area.X - r.X, Math.Max(area.X - r.X, area.Right - r.Right));
            var dy = Math.Clamp(delta.Y, area.Y - r.Y, Math.Max(area.Y - r.Y, area.Bottom - r.Y - height));
            _vm.UpdateInteraction(d with { TextX = d.TextX + dx, TextY = d.TextY + dy });
        } else if (layers.Background is { } bg) {
            var width = d.TemplateId == "split" ? 504d : 1200;
            var scale = Math.Max(width / bg.PixelSize.Width, 900d / bg.PixelSize.Height) * d.Zoom;
            var rx = Math.Max(0, (bg.PixelSize.Width * scale - width) / 2); var ry = Math.Max(0, (bg.PixelSize.Height * scale - 900) / 2);
            _vm.UpdateInteraction(d with { BackgroundX = Math.Clamp(d.BackgroundX + delta.X, -rx, rx), BackgroundY = Math.Clamp(d.BackgroundY + delta.Y, -ry, ry) });
        }
        InteractionFrames++;
    }
    private void Finish(bool commit) {
        if (_start == null) return;
        _start = null; _origin = null; var pointer = _pointer; _pointer = null;
        _vm?.EndInteraction(commit); pointer?.Capture(null); InvalidateVisual();
    }
    private void Release(object? sender, PointerReleasedEventArgs e) { Finish(true); e.Pointer.Capture(null); e.Handled = true; }
}
