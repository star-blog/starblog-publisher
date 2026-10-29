using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Models;

/// <summary>Three views share these GPU-uploaded layers; moving them requires no image encoding or layout.</summary>
public sealed class CoverPreviewLayers : IDisposable {
    private sealed class SharedImage(Bitmap image) {
        public Bitmap Image { get; } = image;
        public int Users = 1;
    }
    private readonly SharedImage? _background;
    private bool _disposed;
    public CoverPreviewLayerData Data { get; }
    public Bitmap? Background => _background?.Image;
    public Bitmap Decoration { get; }
    public Bitmap Text { get; }
    public CoverPreviewLayers(CoverPreviewLayerData data, CoverPreviewLayers? previous = null) {
        Data = data;
        if (data.Background != null) {
            if (previous?._background is { } shared && ReferenceEquals(previous.Data.Background, data.Background)) { _background = shared; shared.Users++; }
            else _background = new(Upload(data.Background));
        }
        Decoration = Upload(data.Decoration); Text = Upload(data.Text);
    }
    private static Bitmap Upload(CoverRawImage data) {
        var bitmap = new WriteableBitmap(new PixelSize(data.Width, data.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var buffer = bitmap.Lock();
        for (var row = 0; row < data.Height; row++) Marshal.Copy(data.Pixels, row * data.Stride, buffer.Address + row * buffer.RowBytes, data.Width * 4);
        return bitmap;
    }
    public void Dispose() { if (_disposed) return; _disposed = true; if (_background != null && --_background.Users == 0) _background.Image.Dispose(); Decoration.Dispose(); Text.Dispose(); }
}
