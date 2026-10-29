using System;
using System.IO;
using SkiaSharp;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Services.Imaging;

internal enum ImageDecodeKind {
    Unrecognized,
    Invalid
}

internal sealed class ImageDecodeException : Exception {
    public ImageDecodeException(ImageDecodeKind kind) : base(kind == ImageDecodeKind.Unrecognized
        ? "Unrecognized image format."
        : "Invalid image content.") {
        Kind = kind;
    }

    public ImageDecodeKind Kind { get; }
}

/// <summary>One decoded frame in straight RGBA, with EXIF orientation already applied.</summary>
internal sealed class DecodedImage : IDisposable {
    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);
    private SKBitmap _bitmap;
    private bool _disposed;

    private DecodedImage(SKBitmap bitmap, SKEncodedImageFormat format) {
        _bitmap = bitmap;
        Format = format;
    }

    public SKEncodedImageFormat Format { get; }
    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public bool HasAlphaChannel => _bitmap.AlphaType != SKAlphaType.Opaque;

    public static DecodedImage Decode(string path) {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    public static DecodedImage Decode(byte[] bytes) {
        using var stream = new MemoryStream(bytes, writable: false);
        return Decode(stream);
    }

    public static DecodedImage Decode(Stream stream) {
        if (stream.CanSeek) stream.Position = 0;
        using var codec = SKCodec.Create(stream) ?? throw new ImageDecodeException(ImageDecodeKind.Unrecognized);
        var oriented = DecodeOriented(codec);
        return new DecodedImage(oriented, codec.EncodedFormat);
    }

    public bool HasTransparentPixel() {
        if (!HasAlphaChannel) return false;
        var span = _bitmap.GetPixelSpan();
        var rowBytes = _bitmap.RowBytes;
        for (var y = 0; y < _bitmap.Height; y++) {
            var row = y * rowBytes;
            for (var x = 0; x < _bitmap.Width; x++) {
                if (span[row + x * 4 + 3] < 255) return true;
            }
        }

        return false;
    }

    public CoverPixel Pixel(int x, int y) {
        var span = _bitmap.GetPixelSpan();
        var index = y * _bitmap.RowBytes + x * 4;
        return new CoverPixel(span[index], span[index + 1], span[index + 2], span[index + 3]);
    }

    public void Resize(int width, int height) {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (width == _bitmap.Width && height == _bitmap.Height) return;
        var destination = new SKBitmap(NewInfo(width, height, _bitmap.AlphaType));
        if (!_bitmap.ScalePixels(destination, Sampling)) {
            destination.Dispose();
            throw new ImageDecodeException(ImageDecodeKind.Invalid);
        }

        _bitmap.Dispose();
        _bitmap = destination;
    }

    public void Cover(int width, int height) {
        var scale = Math.Max(width / (float)_bitmap.Width, height / (float)_bitmap.Height);
        var drawnWidth = _bitmap.Width * scale;
        var drawnHeight = _bitmap.Height * scale;
        var left = (width - drawnWidth) / 2f;
        var top = (height - drawnHeight) / 2f;
        var destination = new SKBitmap(NewInfo(width, height, _bitmap.AlphaType));
        using (var canvas = new SKCanvas(destination))
        using (var image = SKImage.FromBitmap(_bitmap))
        using (var paint = new SKPaint { IsAntialias = true }) {
            canvas.DrawImage(
                image,
                new SKRect(0, 0, _bitmap.Width, _bitmap.Height),
                new SKRect(left, top, left + drawnWidth, top + drawnHeight),
                Sampling,
                paint);
        }

        _bitmap.Dispose();
        _bitmap = destination;
    }

    public DecodedImage Crop(int x, int y, int width, int height) {
        var destination = new SKBitmap(NewInfo(width, height, _bitmap.AlphaType));
        if (!_bitmap.ExtractSubset(destination, new SKRectI(x, y, x + width, y + height))) {
            destination.Dispose();
            throw new ImageDecodeException(ImageDecodeKind.Invalid);
        }

        return new DecodedImage(destination, Format);
    }

    public CoverBitmap DetachCover() {
        var cover = new CoverBitmap(_bitmap);
        _bitmap = null!;
        _disposed = true;
        return cover;
    }

    public void DrawOnto(SKCanvas canvas, SKColor fallback) {
        canvas.Clear(fallback);
        canvas.DrawBitmap(_bitmap, 0, 0);
    }

    public byte[] EncodePng() => Encode(SKEncodedImageFormat.Png, 100);

    public byte[] Encode(SKEncodedImageFormat format, int quality) {
        if (format == SKEncodedImageFormat.Gif) return GifEncoder.Encode(_bitmap);
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(format, quality) ?? throw new ImageDecodeException(ImageDecodeKind.Invalid);
        return data.ToArray();
    }

    public void Save(string path, SKEncodedImageFormat format, int quality) {
        var bytes = Encode(format, quality);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose() {
        if (_disposed) return;
        _bitmap.Dispose();
        _disposed = true;
    }

    internal static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin) {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return source;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var destination = new SKBitmap(NewInfo(swap ? source.Height : source.Width, swap ? source.Width : source.Height, source.AlphaType));
        var sourcePixels = source.GetPixelSpan();
        var destinationPixels = destination.GetPixelSpan();
        var sourceRow = source.RowBytes;
        var destinationRow = destination.RowBytes;
        for (var y = 0; y < source.Height; y++) {
            for (var x = 0; x < source.Width; x++) {
                Map(origin, x, y, source.Width, source.Height, out var dx, out var dy);
                var from = y * sourceRow + x * 4;
                var to = dy * destinationRow + dx * 4;
                destinationPixels[to] = sourcePixels[from];
                destinationPixels[to + 1] = sourcePixels[from + 1];
                destinationPixels[to + 2] = sourcePixels[from + 2];
                destinationPixels[to + 3] = sourcePixels[from + 3];
            }
        }

        source.Dispose();
        return destination;
    }

    private static SKBitmap DecodeOriented(SKCodec codec) {
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0) throw new ImageDecodeException(ImageDecodeKind.Invalid);
        var alpha = codec.Info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
        var info = NewInfo(codec.Info.Width, codec.Info.Height, alpha);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) {
            bitmap.Dispose();
            throw new ImageDecodeException(ImageDecodeKind.Invalid);
        }

        return ApplyOrigin(bitmap, codec.EncodedOrigin);
    }

    private static SKImageInfo NewInfo(int width, int height, SKAlphaType alpha) =>
        new(width, height, SKColorType.Rgba8888, alpha);

    private static void Map(SKEncodedOrigin origin, int x, int y, int width, int height, out int dx, out int dy) {
        switch (origin) {
            case SKEncodedOrigin.TopRight:
                dx = width - 1 - x;
                dy = y;
                break;
            case SKEncodedOrigin.BottomRight:
                dx = width - 1 - x;
                dy = height - 1 - y;
                break;
            case SKEncodedOrigin.BottomLeft:
                dx = x;
                dy = height - 1 - y;
                break;
            case SKEncodedOrigin.LeftTop:
                dx = y;
                dy = x;
                break;
            case SKEncodedOrigin.RightTop:
                dx = height - 1 - y;
                dy = x;
                break;
            case SKEncodedOrigin.RightBottom:
                dx = height - 1 - y;
                dy = width - 1 - x;
                break;
            case SKEncodedOrigin.LeftBottom:
                dx = y;
                dy = width - 1 - x;
                break;
            default:
                dx = x;
                dy = y;
                break;
        }
    }
}
