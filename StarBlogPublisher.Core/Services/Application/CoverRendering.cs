using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SkiaSharp;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Services.Application;

public sealed record CoverDiagnostic(string Message, bool IsError) {
    public CoverElementId Element { get; init; }
    public CoverPreviewFrame? Frame { get; init; }
}
public sealed record CoverTextLine(string Text, float X, float Baseline, int Size, bool Bold) {
    public CoverElementId Element { get; init; } = CoverElementId.Title;
    public string? Color { get; init; }
}
public sealed record CoverLayoutObject(CoverElementId Id, CoverPixelRect Bounds, int FontSize) {
    public CoverPixelRect? TextBox { get; init; }
}
public sealed record CoverLayout(CoverPixelRect TextArea, IReadOnlyList<CoverTextLine> Lines, IReadOnlyList<CoverDiagnostic> Diagnostics, int FontSize) {
    public double EffectiveTextX { get; init; }
    public double EffectiveTextY { get; init; }
    public IReadOnlyList<CoverLayoutObject> Objects { get; init; } = [];
    public CoverPixelRect BackgroundArea { get; init; } = new(0, 0, 1200, 900);
}

public static class CoverLayoutService {
    public static CoverLayout Measure(CoverDocument d, CancellationToken token = default) {
        if (d.Elements != null) return CoverObjectLayout.Measure(d, token) with { BackgroundArea = CoverTemplateCatalog.BackgroundArea(d) };
        var legacy = CoverObjectLayout.WithObjects(MeasureLegacy(d, token));
        legacy = legacy with { BackgroundArea = CoverTemplateCatalog.BackgroundArea(d) };
        return d.Logo == null ? legacy : legacy with { Objects = [.. legacy.Objects, new(CoverElementId.Logo, new(legacy.TextArea.Right - 64, legacy.TextArea.Y, 64, 40), 0)] };
    }
    private static CoverLayout MeasureLegacy(CoverDocument d, CancellationToken token) {
        if (d.Width != 1200 || d.Height != 900 || !d.Crops.IsValid || !double.IsFinite(d.Zoom) || d.Zoom < 1 || d.Zoom > 4
            || !double.IsFinite(d.TextWidth) || d.TextWidth < .25 || d.TextWidth > 1
            || !double.IsFinite(d.LineHeight) || d.LineHeight < 1 || d.LineHeight > 2
            || new[] { d.TextX, d.TextY, d.BackgroundX, d.BackgroundY, d.Scrim }.Any(v => !double.IsFinite(v)))
            throw new InvalidOperationException("封面布局参数无效");
        if (!CoverTemplate.All.Any(t => t.Id == d.TemplateId) || d.TemplateVersion != 1)
            throw new InvalidOperationException("暂不支持此封面模板版本");
        var shared = CoverSafeZone.Intersect(d.Crops.Window(d.Width, d.Height, true), d.Crops.Window(d.Width, d.Height, false));
        var area = CoverSafeZone.Inset(d.Manual ? new(0, 0, d.Width, d.Height) : shared, 48);
        var bounds = area;
        var width = Math.Max(1, (int)(area.Width * d.TextWidth));
        var x = area.X + (d.Centered ? (area.Width - width) / 2 : 0);
        x = Math.Clamp(x, area.X, area.Right - width);
        area = new(x, area.Y, width, area.Height);
        var diagnostics = new List<CoverDiagnostic>();
        if (string.IsNullOrWhiteSpace(d.Title + d.Subtitle + d.Column)) return new(area, [], diagnostics, 0);
        if (!CoverComposer.TryResolveFont(d.Bold, out var face, out _)) throw new InvalidOperationException("找不到可用的中文字体，请安装微软雅黑、苹方或 Noto Sans CJK");
        using (face) {
            var size = Math.Clamp(d.FontSize, 24, 96);
            var lines = new List<(string Text, int Size, bool Bold, CoverElementId Element)>();
            float height;
            while (true) {
                token.ThrowIfCancellationRequested();
                lines.Clear();
                void Add(string text, int s, bool bold, CoverElementId element) {
                    if (string.IsNullOrWhiteSpace(text)) return;
                    using var font = new SKFont(face, s);
                    lines.AddRange(CoverComposer.Wrap(font, text.Replace("\r", ""), width).Select(t => (t, s, bold, element)));
                }
                Add(d.Column, Math.Max(20, size / 2), false, CoverElementId.Column);
                Add(d.Title, size, d.Bold, CoverElementId.Title);
                Add(d.Subtitle, Math.Max(20, size / 2), false, CoverElementId.Subtitle);
                height = lines.Sum(l => l.Size * (float)d.LineHeight * 1.2f);
                var fits = lines.All(l => { using var f = new SKFont(face, l.Size); return f.MeasureText(l.Text) <= width + .5; });
                if (height <= area.Height && fits) break;
                if (size <= 24) { diagnostics.Add(new("文字放不下，请缩短标题、调整换行或更换模板", true)); break; }
                size -= 2;
            }
            var top = d.Placement == "top" ? area.Y : d.Placement == "bottom" ? area.Bottom - height : area.Y + (area.Height - height) / 2;
            var anchorTop = top;
            top = Math.Clamp(top + (float)d.TextY, area.Y, Math.Max(area.Y, area.Bottom - height));
            var output = new List<CoverTextLine>();
            var effectiveY = top - anchorTop;
            float effectiveX = 0;
            foreach (var l in lines) {
                using var f = new SKFont(face, l.Size);
                var lx = d.Centered ? area.X + (width - f.MeasureText(l.Text)) / 2 : area.X;
                output.Add(new(l.Text, lx, top - f.Metrics.Ascent, l.Size, l.Bold) { Element = l.Element });
                top += l.Size * (float)d.LineHeight * 1.2f;
            }
            if (output.Count > 0) {
                var left = output.Min(l => l.X);
                var right = output.Max(l => { using var f = new SKFont(face, l.Size); return l.X + f.MeasureText(l.Text); });
                var dx = Math.Clamp((float)d.TextX, bounds.X - left, Math.Max(bounds.X - left, bounds.Right - right));
                effectiveX = dx;
                for (var i = 0; i < output.Count; i++) output[i] = output[i] with { X = output[i].X + dx };
            }
            if (d.Manual && output.Any(l => {
                using var f = new SKFont(face, l.Size);
                return l.X < shared.X || l.X + f.MeasureText(l.Text) > shared.Right || l.Baseline + f.Metrics.Ascent < shared.Y || l.Baseline + f.Metrics.Descent > shared.Bottom;
            })) diagnostics.Add(new("手动构图中的文字会在部分封面比例中被裁掉，请检查下方预览", false));
            return new(area, output, diagnostics, size) { EffectiveTextX = effectiveX, EffectiveTextY = effectiveY };
        }
    }
}

public sealed class CoverRenderResult(CoverBitmap bitmap, CoverLayout layout) : IDisposable {
    public int BackgroundWidth { get; init; }
    public int BackgroundHeight { get; init; }
    public CoverBitmap Bitmap { get; } = bitmap;
    public CoverLayout Layout { get; } = layout;
    public void Dispose() => Bitmap.Dispose();
}

public enum CoverRenderLayer { Full, Text, Decoration }
public sealed class CoverRenderer {
    public CoverRenderResult Render(CoverDocument d, string? articlePath, CancellationToken token = default, CoverRenderLayer layer = CoverRenderLayer.Full, CoverElementId? element = null, CoverLayout? measured = null) {
        var layout = measured ?? CoverLayoutService.Measure(d, token);
        if (!SKColor.TryParse(d.Color, out var textColor) || !SKColor.TryParse(d.BackgroundColor, out var backgroundColor))
            throw new InvalidOperationException("请输入有效的十六进制颜色");
        var bitmap = new SKBitmap(new SKImageInfo(d.Width, d.Height, SKColorType.Rgba8888, layer == CoverRenderLayer.Full ? SKAlphaType.Opaque : SKAlphaType.Unpremul));
        try {
            var backgroundWidth = 0; var backgroundHeight = 0;
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(layer == CoverRenderLayer.Full ? backgroundColor : SKColors.Transparent);
            if (layer == CoverRenderLayer.Full && d.Background != null && d.UsesBackgroundImage) {
                using var decoded = DecodedImage.Decode(CoverAssetService.Resolve(d.Background, articlePath));
                using var source = decoded.DetachCover();
                backgroundWidth = source.Width; backgroundHeight = source.Height;
                var imageArea = layout.BackgroundArea;
                var target = new SKRect(imageArea.X, imageArea.Y, imageArea.Right, imageArea.Bottom);
                var placement = CoverImageGeometry.Place(d, imageArea, source.Width, source.Height);
                canvas.Save(); canvas.ClipRect(target);
                using var image = SKImage.FromBitmap(source.Native);
                canvas.DrawImage(image, new SKRect(0, 0, source.Width, source.Height), new SKRect(placement.X, placement.Y, placement.X + placement.Width, placement.Y + placement.Height),
                    new SKSamplingOptions(SKCubicResampler.Mitchell));
                canvas.Restore();
            }
            if (layer == CoverRenderLayer.Full && d.Scrim > 0) {
                using var shader = SKShader.CreateLinearGradient(new(0, 0), new(0, d.Height),
                    [SKColors.Transparent, new SKColor(0, 0, 0, (byte)(Math.Clamp(d.Scrim, 0, 1) * 255))], SKShaderTileMode.Clamp);
                using var scrim = new SKPaint { Shader = shader }; canvas.DrawRect(0, 0, d.Width, d.Height, scrim);
            }
            if (layer != CoverRenderLayer.Text && element != CoverElementId.Logo && d.TemplateVersion == 1 && d.TemplateId is "minimal" or "column") {
                using var accent = new SKPaint { Color = SKColor.Parse("#52D6B0"), IsAntialias = true };
                var decorationArea = d.Elements?.LegacyDecorationArea ?? layout.TextArea;
                canvas.DrawRoundRect(decorationArea.X, decorationArea.Y - 16, 80, 6, 3, 3, accent);
            }
            if (layer != CoverRenderLayer.Text && element != CoverElementId.Logo && d.TemplateVersion == 2) {
                using var accent = new SKPaint { Color = SKColor.Parse(d.TemplateId == "minimal" ? "#B65D3A" : d.TemplateId == "split" ? "#4169B3" : "#D7B477"), IsAntialias = true };
                if (d.TemplateId == "minimal") canvas.DrawRect(216, 286, 72, 5, accent);
                if (d.TemplateId == "split") canvas.DrawRect(184, 250, 4, 406, accent);
                if (d.TemplateId == "column") canvas.DrawRect(0, 416, 1200, 4, accent);
            }
            token.ThrowIfCancellationRequested();
            foreach (var line in layer == CoverRenderLayer.Decoration ? [] : layout.Lines) {
                if (element != null && line.Element != element) continue;
                if (!CoverComposer.TryResolveFont(line.Bold, out var face, out _)) throw new InvalidOperationException("找不到封面字体");
                using (face) using (var font = new SKFont(face, line.Size) { Edging = SKFontEdging.Antialias })
                using (var paint = new SKPaint { Color = line.Color == null ? textColor : SKColor.Parse(line.Color), IsAntialias = true }) canvas.DrawText(line.Text, line.X, line.Baseline, font, paint);
            }
            if (layer != CoverRenderLayer.Text && d.Logo != null && (element == null || element == CoverElementId.Logo)) {
                using var decoded = DecodedImage.Decode(CoverAssetService.Resolve(d.Logo, articlePath));
                using var logo = decoded.DetachCover();
                var box = d.Elements?.Logo ?? new(layout.TextArea.Right - 64, layout.TextArea.Y, 64, 40);
                if (d.Elements != null && layout.Objects.FirstOrDefault(o => o.Id == CoverElementId.Logo) is { } item) box = box with { X = item.Bounds.X, Y = item.Bounds.Y, Width = item.Bounds.Width, Height = item.Bounds.Height };
                var scale = (float)Math.Min(box.Width / logo.Width, box.Height / logo.Height);
                var left = d.Elements == null ? layout.TextArea.Right - logo.Width * scale : box.AlignRight ? box.X + box.Width - logo.Width * scale : box.X;
                canvas.DrawBitmap(logo.Native, new SKRect((float)left, (float)box.Y, (float)left + logo.Width * scale, (float)box.Y + logo.Height * scale));
            }
            return new(new CoverBitmap(bitmap), layout) { BackgroundWidth = backgroundWidth, BackgroundHeight = backgroundHeight };
        } catch { bitmap.Dispose(); throw; }
    }
}

public sealed class CoverExportService {
    public PreparedWeChatCover Export(CoverDocument document, string? articlePath, string path, bool png = false,
        CoverPreviewFrame frame = CoverPreviewFrame.Full, bool weChat = false, CancellationToken token = default) {
        using var result = new CoverRenderer().Render(document, articlePath, token);
        var errors = result.Layout.Diagnostics.Where(d => d.IsError).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(errors[0].Message);
        var rect = frame == CoverPreviewFrame.Full ? new CoverPixelRect(0, 0, document.Width, document.Height) :
            document.Crops.Window(document.Width, document.Height, frame == CoverPreviewFrame.Headline);
        using var source = SKImage.FromBitmap(result.Bitmap.Native);
        using var crop = source.Subset(new SKRectI(rect.X, rect.Y, rect.Right, rect.Bottom));
        byte[] bytes = [];
        foreach (var quality in new[] { 90, 80, 70, 60, 40 }) {
            using var data = crop.Encode(png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, quality);
            bytes = data.ToArray();
            if (!weChat || bytes.Length <= 2 * 1024 * 1024) break;
        }
        if (weChat && bytes.Length > 2 * 1024 * 1024) throw new InvalidOperationException("处理后的封面仍超过 2 MB");
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, bytes);
        return new(path, rect.Width, rect.Height) { Crops = document.Crops };
    }
}
