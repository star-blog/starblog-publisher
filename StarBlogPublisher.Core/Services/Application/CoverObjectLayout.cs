using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SkiaSharp;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

public static class CoverObjectLayout {
    public static string FontFamily(bool bold) {
        if (!CoverComposer.TryResolveFont(bold, out var face, out var family)) throw new InvalidOperationException("找不到封面字体");
        face.Dispose(); return family;
    }
    public static readonly CoverElementId[] TextIds = [CoverElementId.Column, CoverElementId.Title, CoverElementId.Subtitle];
    public static CoverPixelRect Safe(CoverDocument d) => CoverSafeZone.Inset(d.Manual ? new(0, 0, 1200, 900) :
        CoverSafeZone.Intersect(d.Crops.Window(1200, 900, true), d.Crops.Window(1200, 900, false)), 48);
    public static CoverPixelRect Bounds(IEnumerable<CoverTextLine> lines) {
        CoverPixelRect? result = null;
        foreach (var l in lines) {
            if (!CoverComposer.TryResolveFont(l.Bold, out var face, out _)) throw new InvalidOperationException("找不到封面字体");
            using (face) using (var f = new SKFont(face, l.Size)) {
                var x = (int)Math.Floor(l.X); var y = (int)Math.Floor(l.Baseline + f.Metrics.Ascent);
                var r = new CoverPixelRect(x, y, (int)Math.Ceiling(f.MeasureText(l.Text)) + 1, (int)Math.Ceiling(f.Metrics.Descent - f.Metrics.Ascent) + 1);
                result = result is not { } old ? r : new(Math.Min(old.X, r.X), Math.Min(old.Y, r.Y), Math.Max(old.Right, r.Right) - Math.Min(old.X, r.X), Math.Max(old.Bottom, r.Bottom) - Math.Min(old.Y, r.Y));
            }
        }
        return result ?? new();
    }
    public static CoverLayout WithObjects(CoverLayout layout) => layout with {
        Objects = layout.Lines.GroupBy(l => l.Element).Select(g => {
            var bounds = Bounds(g);
            return new CoverLayoutObject(g.Key, bounds, g.First().Size) { TextBox = new((int)(layout.TextArea.X + layout.EffectiveTextX), bounds.Y, layout.TextArea.Width, Math.Max(bounds.Height, 40)) };
        }).ToArray()
    };
    public static CoverLayout Measure(CoverDocument d, CancellationToken token) {
        if (!d.IsSupported || d.Width != 1200 || d.Height != 900 || !d.Crops.IsValid || !double.IsFinite(d.Zoom) || d.Zoom is < 1 or > 4 ||
            new[] { d.BackgroundX, d.BackgroundY, d.Scrim }.Any(v => !double.IsFinite(v))) throw new InvalidOperationException("封面布局参数无效");
        var area = Safe(d); var output = new List<CoverTextLine>(); var diagnostics = new List<CoverDiagnostic>();
        var boxes = new Dictionary<CoverElementId, CoverPixelRect>();
        if (d.TemplateVersion == 2 && d.TemplateId is "photo" or "split" && d.Background == null)
            diagnostics.Add(new("此模板需要背景图片，请选择文章图片或本地图片", true) { Element = CoverElementId.Background });
        foreach (var id in TextIds) {
            var style = d.Elements!.Text(id); var content = d.Content(id);
            if (new[] { style.X, style.Y, style.Width, style.Height, style.LineHeight }.Any(v => !double.IsFinite(v)) ||
                style.Width is < 24 or > 1200 || style.Height is < 20 or > 900 || style.LineHeight is < 1 or > 2 || style.FontSize is < 20 or > 96 || !SKColor.TryParse(style.Color, out _))
                throw new InvalidOperationException("文字布局参数无效");
            if (string.IsNullOrWhiteSpace(content)) continue;
            if (!CoverComposer.TryResolveFont(style.Bold, out var face, out _)) throw new InvalidOperationException("找不到可用的中文字体");
            using (face) {
                var minimum = id == CoverElementId.Title ? 24 : 20; var size = style.FontSize;
                var width = Math.Min(style.Width, area.Width); var height = Math.Min(style.Height, area.Height);
                string[] lines;
                while (true) {
                    token.ThrowIfCancellationRequested(); using var font = new SKFont(face, size);
                    lines = CoverComposer.Wrap(font, content.Replace("\r", ""), (int)width).ToArray();
                    if (lines.Length * size * style.LineHeight * 1.2 <= height && lines.All(l => font.MeasureText(l) <= width + .5)) break;
                    if (size <= minimum) { diagnostics.Add(new("文字放不下，请缩短文案、调整换行或文本框", true) { Element = id }); break; }
                    size = Math.Max(minimum, size - 2);
                }
                using var f = new SKFont(face, size);
                var local = new List<CoverTextLine>(); var y = style.Y;
                foreach (var text in lines) {
                    var measured = f.MeasureText(text);
                    var x = style.X + (style.Alignment == CoverAlignment.Center ? (width - measured) / 2 : style.Alignment == CoverAlignment.Right ? width - measured : 0);
                    local.Add(new(text, (float)x, (float)y - f.Metrics.Ascent, size, style.Bold) { Element = id, Color = style.Color });
                    y += size * style.LineHeight * 1.2;
                }
                var bounds = Bounds(local);
                var dx = Math.Clamp(0, area.X - bounds.X, Math.Max(area.X - bounds.X, area.Right - bounds.Right));
                var dy = Math.Clamp(0, area.Y - bounds.Y, Math.Max(area.Y - bounds.Y, area.Bottom - bounds.Bottom));
                boxes[id] = new((int)(style.X + dx), (int)(style.Y + dy), (int)width, (int)height);
                output.AddRange(local.Select(l => l with { X = l.X + dx, Baseline = l.Baseline + dy }));
            }
        }
        var layout = WithObjects(new(area, output, diagnostics, output.FirstOrDefault(l => l.Element == CoverElementId.Title)?.Size ?? 0));
        layout = layout with { Objects = layout.Objects.Select(o => o with { TextBox = boxes.GetValueOrDefault(o.Id) }).ToArray() };
        if (d.Logo != null) {
            var logo = d.Elements!.Logo;
            if (new[] { logo.X, logo.Y, logo.Width, logo.Height }.Any(v => !double.IsFinite(v)) || logo.Width is < 8 or > 900 || logo.Height is < 8 or > 900)
                throw new InvalidOperationException("标识布局参数无效");
            var w = Math.Min(logo.Width, area.Width); var h = Math.Min(logo.Height, area.Height);
            var x = Math.Clamp(logo.X, area.X, Math.Max(area.X, area.Right - w)); var y = Math.Clamp(logo.Y, area.Y, Math.Max(area.Y, area.Bottom - h));
            layout = layout with { Objects = [.. layout.Objects, new(CoverElementId.Logo, new((int)x, (int)y, (int)w, (int)h), 0)] };
        }
        foreach (var obj in layout.Objects) foreach (var frame in new[] { CoverPreviewFrame.Headline, CoverPreviewFrame.Secondary }) {
            var crop = d.Crops.Window(1200, 900, frame == CoverPreviewFrame.Headline); var b = obj.Bounds;
            if (b.X < crop.X || b.Y < crop.Y || b.Right > crop.Right || b.Bottom > crop.Bottom)
                diagnostics.Add(new("此对象在该比例中会被裁切", false) { Element = obj.Id, Frame = frame });
        }
        return layout;
    }
    public static CoverDocument Upgrade(CoverDocument d) {
        if (d.Elements != null) return d;
        var layout = CoverLayoutService.Measure(d);
        CoverTextElement Element(CoverElementId id) {
            var lines = layout.Lines.Where(l => l.Element == id).ToArray(); var bounds = Bounds(lines);
            return new() { X = layout.TextArea.X + layout.EffectiveTextX, Y = lines.Length == 0 ? layout.TextArea.Y : bounds.Y,
                Width = layout.TextArea.Width, Height = Math.Max(40, lines.Sum(l => l.Size * d.LineHeight * 1.2)),
                FontSize = lines.FirstOrDefault()?.Size ?? (id == CoverElementId.Title ? d.FontSize : Math.Max(20, d.FontSize / 2)),
                Bold = id == CoverElementId.Title && d.Bold, Color = d.Color, LineHeight = d.LineHeight,
                Alignment = d.Centered ? CoverAlignment.Center : CoverAlignment.Left };
        }
        return d with { Elements = new() { Title = Element(CoverElementId.Title), Subtitle = Element(CoverElementId.Subtitle), Column = Element(CoverElementId.Column), Logo = new(layout.TextArea.Right - 64, layout.TextArea.Y, AlignRight: true), LegacyDecorationArea = layout.TextArea } };
    }
    public static CoverDocument FreezePositions(CoverDocument d, CoverLayout layout) {
        if (d.Elements is not { } elements) return d;
        foreach (var id in TextIds) {
            var style = elements.Text(id); var lines = layout.Lines.Where(l => l.Element == id).ToArray();
            if (lines.Length == 0) continue;
            using var face = SKTypeface.FromFamilyName(FontFamily(style.Bold), style.Bold ? SKFontStyle.Bold : SKFontStyle.Normal);
            using var font = new SKFont(face, lines[0].Size);
            var width = Math.Min(style.Width, layout.TextArea.Width); var first = lines[0]; var measured = font.MeasureText(first.Text);
            var x = first.X - (style.Alignment == CoverAlignment.Center ? (width - measured) / 2 : style.Alignment == CoverAlignment.Right ? width - measured : 0);
            elements = elements.With(id, style with { X = x, Y = first.Baseline + font.Metrics.Ascent, Width = width, Height = Math.Min(style.Height, layout.TextArea.Height), FontSize = first.Size });
        }
        if (layout.Objects.FirstOrDefault(o => o.Id == CoverElementId.Logo) is { } logo) elements = elements with { Logo = elements.Logo with { X = logo.Bounds.X, Y = logo.Bounds.Y, Width = logo.Bounds.Width, Height = logo.Bounds.Height } };
        return d with { Elements = elements };
    }
}
