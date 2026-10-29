using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Imaging;
using SkiaSharp;

namespace StarBlogPublisher.Services.Application;

public sealed record CoverRawImage(int Width, int Height, int Stride, byte[] Pixels);
public sealed record CoverPreviewLayerData(CoverDocument Document, CoverRawImage? Background, CoverRawImage Decoration,
    CoverRawImage Text, CoverLayout Layout, CoverPixelRect TextBounds) {
    public IReadOnlyDictionary<CoverElementId, CoverRawImage> ObjectLayers { get; init; } = new Dictionary<CoverElementId, CoverRawImage>();
    public static CoverPreviewLayerData Create(CoverDocument document, string? articlePath, CoverRawImage? cachedBackground, CancellationToken token, CoverPreviewLayerData? previous = null) {
        var background = document.UsesBackgroundImage ? cachedBackground : null;
        if (document.UsesBackgroundImage && document.Background != null && background == null) {
            using var decoded = DecodedImage.Decode(CoverAssetService.Resolve(document.Background, articlePath));
            using var bitmap = decoded.DetachCover();
            background = bitmap.CopyPixels();
        }
        token.ThrowIfCancellationRequested();
        var renderer = new CoverRenderer();
        var layout = CoverLayoutService.Measure(document, token);
        using var text = renderer.Render(document, articlePath, token, CoverRenderLayer.Text, measured: layout);
        using var decoration = renderer.Render(document, articlePath, token, CoverRenderLayer.Decoration, CoverElementId.None, layout);
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
        var objects = new Dictionary<CoverElementId, CoverRawImage>();
        foreach (var obj in layout.Objects) {
            var old = previous?.Document;
            var reusable = old?.Elements != null && document.Elements != null && old.Crops == document.Crops && old.Manual == document.Manual &&
                (obj.Id == CoverElementId.Logo ? old.Logo == document.Logo && old.Elements.Logo == document.Elements.Logo :
                    old.Elements.Text(obj.Id) == document.Elements.Text(obj.Id) && old.Content(obj.Id) == document.Content(obj.Id));
            if (reusable && previous!.ObjectLayers.TryGetValue(obj.Id, out var cached)) objects[obj.Id] = cached;
            else {
                using var rendered = renderer.Render(document, articlePath, token, obj.Id == CoverElementId.Logo ? CoverRenderLayer.Decoration : CoverRenderLayer.Text, obj.Id, layout);
                objects[obj.Id] = rendered.Bitmap.CopyPixels();
            }
        }
        return new(document, background, decoration.Bitmap.CopyPixels(), text.Bitmap.CopyPixels(), text.Layout, rect) { ObjectLayers = objects };
    }
}
