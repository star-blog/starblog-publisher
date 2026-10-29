using System;
using System.IO;
using System.Linq;
using System.Threading;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Imaging;
using SkiaSharp;

namespace StarBlogPublisher.Services.Application;

public sealed record CoverRawImage(int Width, int Height, int Stride, byte[] Pixels);
public sealed record CoverPreviewLayerData(CoverDocument Document, CoverRawImage? Background, CoverRawImage Decoration,
    CoverRawImage Text, CoverLayout Layout, CoverPixelRect TextBounds) {
    public static CoverPreviewLayerData Create(CoverDocument document, string? articlePath, CoverRawImage? cachedBackground, CancellationToken token) {
        var background = cachedBackground;
        if (document.Background != null && background == null) {
            using var decoded = DecodedImage.Decode(CoverAssetService.Resolve(document.Background, articlePath));
            using var bitmap = decoded.DetachCover();
            background = bitmap.CopyPixels();
        }
        token.ThrowIfCancellationRequested();
        var renderer = new CoverRenderer();
        using var text = renderer.Render(document, articlePath, token, CoverRenderLayer.Text);
        using var decoration = renderer.Render(document, articlePath, token, CoverRenderLayer.Decoration);
        var rect = new CoverPixelRect();
        foreach (var line in text.Layout.Lines) {
            if (!CoverComposer.TryResolveFont(line.Bold, out var face, out _)) continue;
            using (face) using (var font = new SKFont(face, line.Size)) {
                var r = new CoverPixelRect((int)Math.Floor(line.X), (int)Math.Floor(line.Baseline + font.Metrics.Ascent),
                    (int)Math.Ceiling(font.MeasureText(line.Text)) + 1, (int)Math.Ceiling(font.Metrics.Descent - font.Metrics.Ascent) + 1);
                if (rect.Width == 0) rect = r;
                else { var x = Math.Min(rect.X, r.X); var y = Math.Min(rect.Y, r.Y);
                    rect = new(x, y, Math.Max(rect.Right, r.Right) - x, Math.Max(rect.Bottom, r.Bottom) - y); }
            }
        }
        return new(document, background, decoration.Bitmap.CopyPixels(), text.Bitmap.CopyPixels(), text.Layout, rect);
    }
}
