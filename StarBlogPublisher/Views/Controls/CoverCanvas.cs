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

/// <summary>Hit testing and cached composition in master coordinates. Pointer updates never encode or save images.</summary>
public sealed class CoverCanvas : Decorator {
    public static readonly StyledProperty<CoverPreviewFrame> FrameProperty = AvaloniaProperty.Register<CoverCanvas, CoverPreviewFrame>(nameof(Frame));
    public CoverPreviewFrame Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    private CoverStudioViewModel? _vm;
    private Point? _start;
    private Point _startDip;
    private CoverDocument? _origin;
    private CoverElementId _dragId;
    private IPointer? _pointer;
    private bool _dragging, _settingText;
    private readonly TextBox _editor = new() { IsVisible = false, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        IsUndoEnabled = false, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    public int InteractionFrames { get; private set; }
    public CoverCanvas() {
        // Fluent's focus/pointer states paint PART_BorderElement directly, bypassing
        // the TextBox.Background value. Scope these resources to the canvas editor.
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled" })
            _editor.Resources[key] = Brushes.Transparent;
        _editor.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        Focusable = true; ClipToBounds = true; Child = _editor;
        DataContextChanged += (_, _) => Connect(DataContext as CoverStudioViewModel);
        AttachedToVisualTree += (_, _) => Connect(DataContext as CoverStudioViewModel);
        DetachedFromVisualTree += (_, _) => Connect(null);
        PointerPressed += Press; PointerMoved += Move; PointerReleased += (_, e) => { if (_start != null) { Finish(true); e.Handled = true; } };
        PointerCaptureLost += (_, _) => Finish(false);
        KeyDown += OnKey;
        _editor.TextChanged += (_, _) => { if (!_settingText && _vm?.EditorMode == CoverEditorMode.Text) _vm.SelectedContent = _editor.Text ?? ""; };
        _editor.LostFocus += (_, _) => _vm?.EndInlineEdit();
        PointerWheelChanged += (_, e) => {
            if (Frame == CoverPreviewFrame.Full && _vm?.EditorMode == CoverEditorMode.Background && _start == null) {
                _vm.Zoom = Math.Clamp(_vm.Zoom + e.Delta.Y * .1, 1, 4); e.Handled = true;
            }
        };
    }
    private void Connect(CoverStudioViewModel? vm) {
        if (_vm == vm) return;
        Finish(false);
        if (_vm != null) { _vm.PropertyChanged -= Changed; _vm.LiveChanged -= Refresh; _vm.InlineEditRequested -= BeginText; _vm.InlineEditEnded -= EndText; }
        _vm = vm;
        if (vm != null) { vm.PropertyChanged += Changed; vm.LiveChanged += Refresh; vm.InlineEditRequested += BeginText; vm.InlineEditEnded += EndText; }
        InvalidateVisual();
    }
    private void Refresh() { InvalidateVisual(); if (_editor.IsVisible) InvalidateArrange(); }
    private void Changed(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(CoverStudioViewModel.SelectedContent) && _editor.IsVisible && _editor.Text != _vm!.SelectedContent) {
            _settingText = true; _editor.Text = _vm.SelectedContent; _settingText = false;
        }
        Refresh();
    }
    private (double Scale, double X, double Y, CoverPixelRect Crop) Viewport() {
        var d = _vm!.LiveDocument;
        var crop = Frame == CoverPreviewFrame.Full ? new CoverPixelRect(0, 0, 1200, 900) : d.Crops.Window(1200, 900, Frame == CoverPreviewFrame.Headline);
        var scale = Math.Min(Bounds.Width / crop.Width, Bounds.Height / crop.Height);
        return (scale, (Bounds.Width - crop.Width * scale) / 2 - crop.X * scale, (Bounds.Height - crop.Height * scale) / 2 - crop.Y * scale, crop);
    }
    public Rect MasterRect(CoverPixelRect r) => Map(r.X, r.Y, r.Width, r.Height);
    private Rect Map(double x, double y, double w, double h) { var v = Viewport(); return new(v.X + x * v.Scale, v.Y + y * v.Scale, w * v.Scale, h * v.Scale); }
    private (double X, double Y) Offset(CoverElementId id) {
        var current = _vm!.LiveDocument; var old = _vm.Preview!.Data.Document;
        if (current.Elements is { } c && old.Elements is { } o) return id == CoverElementId.Logo ?
            (c.Logo.X - o.Logo.X, c.Logo.Y - o.Logo.Y) : (c.Text(id).X - o.Text(id).X, c.Text(id).Y - o.Text(id).Y);
        return (current.TextX - (_vm.IsInteracting ? _vm.Preview.Data.Layout.EffectiveTextX : old.TextX),
            current.TextY - (_vm.IsInteracting ? _vm.Preview.Data.Layout.EffectiveTextY : old.TextY));
    }
    public override void Render(DrawingContext context) {
        if (_vm?.Preview is not { } layers) return;
        var d = _vm.LiveDocument; if (Viewport().Scale <= 0) return;
        using (context.PushClip(MasterRect(Viewport().Crop))) {
            context.FillRectangle(new SolidColorBrush(Color.TryParse(d.BackgroundColor, out var c) ? c : Colors.Black), Map(0, 0, 1200, 900));
            if (layers.Background is { } bg && d.UsesBackgroundImage) {
                var target = layers.Data.Layout.BackgroundArea;
                var p = CoverImageGeometry.Place(d, target, bg.PixelSize.Width, bg.PixelSize.Height);
                using (context.PushClip(MasterRect(target))) context.DrawImage(bg, new Rect(bg.Size), Map(p.X, p.Y, p.Width, p.Height));
            }
            if (d.Scrim > 0) {
                var brush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative) };
                brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0)); brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(Math.Clamp(d.Scrim, 0, 1) * 255), 0, 0, 0), 1));
                context.FillRectangle(brush, Map(0, 0, 1200, 900));
            }
            context.DrawImage(layers.Decoration, Map(0, 0, 1200, 900));
            if (layers.Data.ObjectLayers.Count > 0) {
                foreach (var obj in layers.Data.Layout.Objects) {
                    if ((Frame == CoverPreviewFrame.Full && _vm.EditorMode == CoverEditorMode.Text && obj.Id == _vm.SelectedElement) || !layers.Data.ObjectLayers.ContainsKey(obj.Id)) continue;
                    var delta = Offset(obj.Id); context.DrawImage(layers.Object(obj.Id), Map(delta.X, delta.Y, 1200, 900));
                }
            } else { var delta = Offset(CoverElementId.Title); context.DrawImage(layers.Text, Map(delta.X, delta.Y, 1200, 900)); }
            if (Frame != CoverPreviewFrame.Full) return;
            if (_vm.IsCropping) {
                var crop = d.Crops.Window(1200, 900, _vm.EditorMode == CoverEditorMode.Headline); var dim = new SolidColorBrush(Colors.Black, .35);
                context.FillRectangle(dim, Map(0, 0, 1200, crop.Y)); context.FillRectangle(dim, Map(0, crop.Bottom, 1200, 900 - crop.Bottom));
                context.FillRectangle(dim, Map(0, crop.Y, crop.X, crop.Height)); context.FillRectangle(dim, Map(crop.Right, crop.Y, 1200 - crop.Right, crop.Height));
                context.DrawRectangle(null, new Pen(Brushes.White, 1.5), MasterRect(crop));
            } else if (_vm.ShowGuides) {
                var pen = new Pen(new SolidColorBrush(Colors.White, .6), 1, new DashStyle([4, 4], 0));
                foreach (var r in new[] { d.Crops.Window(1200, 900, true), d.Crops.Window(1200, 900, false) }) context.DrawRectangle(null, pen, MasterRect(r));
            }
            if (!_vm.IsComposing && _vm.SelectedElement != CoverElementId.None) {
                var selected = layers.Data.Layout.Objects.FirstOrDefault(o => o.Id == _vm.SelectedElement);
                if (selected != null) {
                    var delta = Offset(selected.Id); var b = selected.Bounds;
                    context.DrawRectangle(null, new Pen((IBrush?)this.FindResource("AccentFillColorDefaultBrush") ?? Brushes.CornflowerBlue, 1), Map(b.X + delta.X - 5, b.Y + delta.Y - 5, b.Width + 10, b.Height + 10), 2, 2);
                }
            }
            if (d.TemplateVersion == 2 && d.TemplateId is "photo" or "split" && layers.Background == null) {
                var target = MasterRect(layers.Data.Layout.BackgroundArea);
                var text = new FormattedText("双击此处添加背景图片", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 14, Brushes.Gray);
                context.DrawText(text, new Point(target.Center.X - text.Width / 2, target.Center.Y - text.Height / 2));
            }
        }
    }
    private Point Logical(Point point) { var v = Viewport(); return new((point.X - v.X) / v.Scale, (point.Y - v.Y) / v.Scale); }
    public CoverElementId HitObject(Point logical) => _vm?.Preview?.Data.Layout.Objects.Reverse().FirstOrDefault(o =>
        new Rect(o.Bounds.X - 5, o.Bounds.Y - 5, o.Bounds.Width + 10, o.Bounds.Height + 10).Contains(logical))?.Id ?? CoverElementId.None;
    private void Press(object? sender, PointerPressedEventArgs e) {
        if (_editor.IsKeyboardFocusWithin || Frame != CoverPreviewFrame.Full || _vm?.Preview == null || !_vm.IsEditable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = Logical(e.GetPosition(this)); if (p.X is < 0 or > 1200 || p.Y is < 0 or > 900) return;
        if (!_vm.IsComposing) {
            var id = HitObject(p);
            if (e.ClickCount == 2) {
                if (id is CoverElementId.Title or CoverElementId.Subtitle or CoverElementId.Column) _vm.StartInlineEdit(id);
                else if (_vm.HasBackground && _vm.CanUseBackground) _vm.AdjustBackgroundCommand.Execute(null);
                else { _vm.SelectObjectCommand.Execute(CoverElementId.Background); if (_vm.CanUseBackground) _vm.ShowArticleImagesCommand.Execute(null); }
                e.Handled = true; return;
            }
            _vm.SelectObjectCommand.Execute(id); _dragId = id;
            if (id is CoverElementId.None or CoverElementId.Background) { Focus(); return; }
        }
        _start = p; _startDip = e.GetPosition(this); _origin = _vm.LiveDocument;
        if (_origin.Elements == null && !_vm.IsComposing) _origin = _origin with { TextX = _vm.Preview.Data.Layout.EffectiveTextX, TextY = _vm.Preview.Data.Layout.EffectiveTextY };
        _pointer = e.Pointer; _dragging = false; InteractionFrames = 0;
        Focus(); e.Pointer.Capture(this); e.Handled = true;
    }
    private void Move(object? sender, PointerEventArgs e) {
        if (_vm == null || _start is not { } start || _origin is not { } d || _vm.Preview is not { } layers) return;
        var distance = e.GetPosition(this) - _startDip;
        if (!_dragging && distance.X * distance.X + distance.Y * distance.Y < 16) return;
        if (!_dragging) { if (!_vm.IsComposing && !_vm.BeginInteraction()) return; _dragging = true; }
        var delta = Logical(e.GetPosition(this)) - start;
        if (_vm.EditorMode == CoverEditorMode.Background && layers.Background is { } bg) {
            var placement = CoverImageGeometry.Place(d, layers.Data.Layout.BackgroundArea, bg.PixelSize.Width, bg.PixelSize.Height);
            var rx = placement.PanX; var ry = placement.PanY;
            _vm.UpdateInteraction(d with { BackgroundX = Math.Clamp(d.BackgroundX + delta.X, -rx, rx), BackgroundY = Math.Clamp(d.BackgroundY + delta.Y, -ry, ry) });
        } else if (_vm.IsCropping) {
            d = _vm.CropOrigin;
            var headline = _vm.EditorMode == CoverEditorMode.Headline; var crop = d.Crops.Window(1200, 900, headline);
            var range = headline ? 900 - crop.Height : 1200 - crop.Width; var offset = range == 0 ? 0 : (headline ? delta.Y : delta.X) / range;
            _vm.UpdateInteraction(d with { Manual = true, Crops = headline ? d.Crops with { HeadlineOffset = Math.Clamp(d.Crops.HeadlineOffset + offset, 0, 1) } :
                d.Crops with { SecondaryOffset = Math.Clamp(d.Crops.SecondaryOffset + offset, 0, 1) } });
        } else _vm.UpdateInteraction(_vm.MoveObject(d, _dragId, delta.X, delta.Y));
        InteractionFrames++;
    }
    private void Finish(bool commit) {
        if (_start == null) return;
        _start = null; _origin = null; var pointer = _pointer; _pointer = null;
        if (!commit && _vm?.IsComposing == true) _vm.CancelCompositionCommand.Execute(null);
        else if (_dragging && _vm?.IsComposing != true) _vm?.EndInteraction(commit);
        _dragging = false; pointer?.Capture(null); InvalidateVisual();
    }
    private void OnKey(object? sender, KeyEventArgs e) {
        if (Frame != CoverPreviewFrame.Full || _vm == null) return;
        if (_editor.IsVisible) {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key == Key.Escape) { _vm.EndInlineEdit(); e.Handled = true; }
            return;
        }
        if (e.Key == Key.Escape) { Finish(false); _vm.CancelCompositionCommand.Execute(null); e.Handled = true; }
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1;
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down) { _vm.Nudge(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0); e.Handled = true; }
    }
    private void BeginText() {
        if (Frame != CoverPreviewFrame.Full || _vm == null) return;
        _settingText = true; _editor.Text = _vm.SelectedContent; _settingText = false;
        _editor.IsVisible = true; InvalidateMeasure(); _editor.Focus(); _editor.CaretIndex = _editor.Text?.Length ?? 0;
    }
    private void EndText() {
        // TextChanged is queued by Avalonia; flush confirmed text before hiding or saving.
        if (_editor.IsVisible && _vm != null && _editor.Text != _vm.SelectedContent) _vm.SelectedContent = _editor.Text ?? "";
        _editor.IsVisible = false; InvalidateMeasure(); InvalidateVisual();
    }
    protected override Size ArrangeOverride(Size finalSize) {
        if (_editor.IsVisible && _vm?.Preview is { } preview) {
            var style = _vm.SelectedTextStyle; var obj = preview.Data.Layout.Objects.FirstOrDefault(o => o.Id == _vm.SelectedElement);
            if (obj != null) {
                var v = Viewport();
                var bold = preview.Data.Layout.Lines.FirstOrDefault(l => l.Element == obj.Id)?.Bold ?? _vm.IsBold;
                _editor.FontSize = Math.Max(8, obj.FontSize * v.Scale); _editor.FontWeight = bold ? FontWeight.Bold : FontWeight.Normal;
                _editor.LineHeight = _editor.FontSize * (style?.LineHeight ?? _vm.LineHeight) * 1.2;
                _editor.Foreground = new SolidColorBrush(_vm.SelectedColor);
                _editor.CaretBrush = _editor.Foreground;
                _editor.TextAlignment = _vm.Alignment == CoverAlignment.Center ? TextAlignment.Center : _vm.Alignment == CoverAlignment.Right ? TextAlignment.Right : TextAlignment.Left;
                _editor.FontFamily = new FontFamily(CoverObjectLayout.FontFamily(bold));
                _editor.Arrange(MasterRect(obj.TextBox ?? obj.Bounds));
            }
        }
        return finalSize;
    }
}
