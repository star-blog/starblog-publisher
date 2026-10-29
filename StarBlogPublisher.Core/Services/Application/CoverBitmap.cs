using System;
using SkiaSharp;

namespace StarBlogPublisher.Services.Application;

public readonly record struct CoverPixel(byte R, byte G, byte B, byte A);

/// <summary>Owned straight-RGBA pixels. The indexer is x, then y.</summary>
public sealed class CoverBitmap : IDisposable {
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    internal CoverBitmap(SKBitmap bitmap) {
        _bitmap = bitmap;
    }

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    public CoverPixel this[int x, int y] {
        get {
            var span = _bitmap.GetPixelSpan();
            var index = y * _bitmap.RowBytes + x * 4;
            return new CoverPixel(span[index], span[index + 1], span[index + 2], span[index + 3]);
        }
    }

    internal byte[] EncodeJpeg(int quality) {
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality) ??
                         throw new InvalidOperationException("无法编码封面 JPEG");
        return data.ToArray();
    }

    public void Dispose() {
        if (_disposed) return;
        _bitmap.Dispose();
        _disposed = true;
    }
}
