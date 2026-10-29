using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Services.Application;

public enum CoverTextColor {
    White,
    Black,
    Custom
}

public enum CoverTextHorizontalAlign {
    Left,
    Center
}

public enum CoverTextVerticalPlacement {
    Top,
    Center,
    Bottom
}

public enum CoverPreviewFrame {
    Full,
    Headline,
    Secondary
}

/// <summary>Background, title, and the small set of text styles baked into one cover.</summary>
public sealed record CoverComposition {
    public string Title { get; init; } = "";
    public int FontSize { get; init; } = CoverComposer.DefaultFontSize;
    public CoverTextColor Color { get; init; } = CoverTextColor.White;
    public string CustomColorHex { get; init; } = "#FFFFFF";
    public bool Bold { get; init; } = true;
    public CoverTextHorizontalAlign Align { get; init; } = CoverTextHorizontalAlign.Center;
    public CoverTextVerticalPlacement Placement { get; init; } = CoverTextVerticalPlacement.Bottom;
    public bool Scrim { get; init; } = true;
}

public readonly record struct ComposedCover(string Path, int Width, int Height, int EffectiveFontSize, string FontFamily);

public sealed class RenderedCover : IDisposable {
    public RenderedCover(CoverBitmap image, int effectiveFontSize, string fontFamily) {
        Image = image;
        EffectiveFontSize = effectiveFontSize;
        FontFamily = fontFamily;
    }

    public CoverBitmap Image { get; }
    public int EffectiveFontSize { get; }
    public string FontFamily { get; }

    public void Dispose() => Image.Dispose();
}

/// <summary>
/// Draws a 1200×900 cover: a cover-fit background, an optional readability scrim, and a title
/// kept inside the area shared by the WeChat headline and secondary center crops.
/// </summary>
public sealed class CoverComposer {
    public const int CanvasWidth = CoverSafeZone.CanvasWidth;
    public const int CanvasHeight = CoverSafeZone.CanvasHeight;
    public const int MinFontSize = 24;
    public const int MaxFontSize = 96;
    public const int DefaultFontSize = 64;
    private const int MaxJpegBytes = 2 * 1024 * 1024;
    private static readonly SKColor FallbackColor = new(0x1F, 0x29, 0x33);
    private static readonly string[] PreferredFontFamilies = [
        "Microsoft YaHei",
        "Microsoft YaHei UI",
        "PingFang SC",
        "PingFang HK",
        "Noto Sans CJK SC",
        "Noto Sans CJK JP",
        "Noto Sans SC",
        "Source Han Sans SC",
        "WenQuanYi Micro Hei"
    ];

    public Task<ComposedCover> ComposeAsync(Stream? background, CoverComposition composition, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        using var rendered = Render(background, composition);
        var directory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "cover-studio");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.jpg");
        SaveJpeg(rendered.Image, path);
        return Task.FromResult(new ComposedCover(path, CanvasWidth, CanvasHeight, rendered.EffectiveFontSize, rendered.FontFamily));
    }

    public Task<RenderedCover> RenderAsync(Stream? background, CoverComposition composition, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Render(background, composition));
    }

    public byte[] CreatePreviewPng(string composedPath, CoverPreviewFrame frame) {
        using var image = DecodedImage.Decode(composedPath);
        var crop = frame switch {
            CoverPreviewFrame.Headline => CoverSafeZone.Headline(image.Width, image.Height),
            CoverPreviewFrame.Secondary => CoverSafeZone.Secondary(image.Width, image.Height),
            _ => new CoverPixelRect(0, 0, image.Width, image.Height)
        };
        using var framed = image.Crop(crop.X, crop.Y, crop.Width, crop.Height);
        return framed.EncodePng();
    }

    private static RenderedCover Render(Stream? background, CoverComposition composition) {
        var bitmap = new SKBitmap(new SKImageInfo(CanvasWidth, CanvasHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        try {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(FallbackColor);
            if (background != null) DrawBackground(canvas, background);

            var title = NormalizeTitle(composition.Title);
            var effectiveSize = 0;
            var fontFamily = "";
            if (title.Length > 0) {
                if (!TryResolveFont(composition.Bold, out var typeface, out fontFamily)) {
                    throw new InvalidOperationException("找不到可用的中文字体。Windows 需要微软雅黑，macOS 需要苹方，Linux 需要 Noto Sans CJK。");
                }

                using (typeface) {
                    var layout = LayoutTitle(typeface, title, composition);
                    effectiveSize = layout.FontSize;
                    DrawTitle(canvas, layout, composition);
                }
            }

            return new RenderedCover(new CoverBitmap(bitmap), effectiveSize, fontFamily);
        }
        catch {
            bitmap.Dispose();
            throw;
        }
    }

    internal static bool TryResolveFont(bool bold, out SKTypeface typeface, out string familyName) {
        typeface = null!;
        familyName = "";
        try {
            var manager = SKFontManager.Default;
            foreach (var name in PreferredFontFamilies) {
                if (TryMatch(manager, name, bold, out typeface)) {
                    familyName = typeface.FamilyName;
                    return true;
                }
            }

            foreach (var candidate in manager.FontFamilies) {
                if (!IsPreferredFamily(candidate)) continue;
                if (TryMatch(manager, candidate, bold, out typeface)) {
                    familyName = typeface.FamilyName;
                    return true;
                }
            }
        }
        catch (Exception) {
            typeface?.Dispose();
            typeface = null!;
            return false;
        }

        return false;
    }

    private static void DrawBackground(SKCanvas canvas, Stream background) {
        DecodedImage source;
        try {
            source = DecodedImage.Decode(background);
        }
        catch (ImageDecodeException ex) {
            throw new InvalidOperationException("无法读取背景图片", ex);
        }

        using (source) {
            source.Cover(CanvasWidth, CanvasHeight);
            source.DrawOnto(canvas, FallbackColor);
        }
    }

    private static TitleLayout LayoutTitle(SKTypeface typeface, string title, CoverComposition composition) {
        var textArea = CoverSafeZone.TextArea();
        var requested = Math.Clamp(composition.FontSize, MinFontSize, MaxFontSize);
        var size = requested;
        SKFont font;
        string[] lines;
        float blockHeight;
        while (true) {
            font = new SKFont(typeface, size) { Edging = SKFontEdging.Antialias };
            lines = Wrap(font, title, textArea.Width);
            blockHeight = Math.Max(1f, lines.Length * font.Spacing);
            if (blockHeight <= textArea.Height + 0.5f || size <= MinFontSize) break;
            font.Dispose();
            size -= 2;
        }

        var y = composition.Placement switch {
            CoverTextVerticalPlacement.Top => textArea.Y,
            CoverTextVerticalPlacement.Center => textArea.Y + (textArea.Height - blockHeight) / 2f,
            _ => textArea.Y + textArea.Height - blockHeight
        };
        return new TitleLayout(title, font, lines, size, y, blockHeight, ResolveColor(composition));
    }

    private static void DrawTitle(SKCanvas canvas, TitleLayout layout, CoverComposition composition) {
        using (layout.Font) {
            if (composition.Scrim) DrawScrim(canvas, composition.Placement, layout.Top, layout.Height);
            var clip = CoverSafeZone.Intersection();
            canvas.Save();
            canvas.ClipRect(new SKRect(clip.X, clip.Y, clip.Right, clip.Bottom));
            using var paint = new SKPaint {
                Color = layout.Color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            var textArea = CoverSafeZone.TextArea();
            var centered = composition.Align == CoverTextHorizontalAlign.Center;
            var metrics = layout.Font.Metrics;
            var y = layout.Top;
            foreach (var line in layout.Lines) {
                var width = layout.Font.MeasureText(line);
                var x = centered ? textArea.X + (textArea.Width - width) / 2f : textArea.X;
                canvas.DrawText(line, x, y - metrics.Ascent, layout.Font, paint);
                y += layout.Font.Spacing;
            }

            canvas.Restore();
        }
    }

    private static void DrawScrim(SKCanvas canvas, CoverTextVerticalPlacement placement, float textTop, float textHeight) {
        const byte alpha = 176;
        var dark = new SKColor(0, 0, 0, alpha);
        var clear = SKColors.Transparent;
        float top;
        float bottom;
        SKColor[] colors;
        float[] positions;
        switch (placement) {
            case CoverTextVerticalPlacement.Top:
                top = 0;
                bottom = Math.Min(CanvasHeight, textTop + textHeight + 80);
                colors = [dark, clear];
                positions = [0, 1];
                break;
            case CoverTextVerticalPlacement.Center:
                top = Math.Max(0, textTop - 48);
                bottom = Math.Min(CanvasHeight, textTop + textHeight + 48);
                colors = [clear, dark, dark, clear];
                positions = [0, 0.35f, 0.65f, 1];
                break;
            default:
                top = Math.Max(0, textTop - 100);
                bottom = CanvasHeight;
                colors = [clear, dark];
                positions = [0, 1];
                break;
        }

        if (bottom - top < 1) return;
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, top),
            new SKPoint(0, bottom),
            colors,
            positions,
            SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawRect(new SKRect(0, top, CanvasWidth, bottom), paint);
    }

    private static string[] Wrap(SKFont font, string title, int maxWidth) {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var paragraph in title.Split('\n')) {
            if (paragraph.Length == 0) {
                lines.Add("");
                continue;
            }

            var start = 0;
            for (var index = 1; index <= paragraph.Length; index++) {
                if (font.MeasureText(paragraph.AsSpan(start, index - start)) <= maxWidth) continue;
                if (index - 1 == start) continue;
                lines.Add(paragraph[start..(index - 1)]);
                start = index - 1;
            }

            lines.Add(paragraph[start..]);
        }

        return lines.ToArray();
    }

    private static bool TryMatch(SKFontManager manager, string family, bool bold, out SKTypeface typeface) {
        typeface = manager.MatchFamily(family, bold ? SKFontStyle.Bold : SKFontStyle.Normal);
        if (!IsRequestedFamily(typeface, family)) {
            typeface?.Dispose();
            typeface = null!;
            return false;
        }

        if (bold && typeface.FontStyle.Weight < (int)SKFontStyleWeight.SemiBold) {
            typeface.Dispose();
            typeface = manager.MatchFamily(family, SKFontStyle.Normal);
            if (!IsRequestedFamily(typeface, family)) {
                typeface?.Dispose();
                typeface = null!;
                return false;
            }
        }

        return true;
    }

    private static bool IsRequestedFamily(SKTypeface? typeface, string requested) {
        if (typeface == null) return false;
        var actual = typeface.FamilyName;
        return actual.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
               requested.Contains(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreferredFamily(string name) =>
        name.Contains("YaHei", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("PingFang", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Noto Sans CJK", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Noto Sans SC", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Source Han Sans", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("WenQuanYi", StringComparison.OrdinalIgnoreCase);

    private static SKColor ResolveColor(CoverComposition composition) {
        if (composition.Color == CoverTextColor.Black) return SKColors.Black;
        if (composition.Color == CoverTextColor.Custom && TryParseHex(composition.CustomColorHex, out var custom)) {
            return custom;
        }

        return SKColors.White;
    }

    private static bool TryParseHex(string hex, out SKColor color) {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var text = hex.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        return SKColor.TryParse(text, out color);
    }

    private static string NormalizeTitle(string? title) {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return title.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    }

    private static void SaveJpeg(CoverBitmap image, string path) {
        var quality = 90;
        while (true) {
            File.WriteAllBytes(path, image.EncodeJpeg(quality));
            if (new FileInfo(path).Length <= MaxJpegBytes || quality <= 60) return;
            quality -= 10;
        }
    }

    private readonly record struct TitleLayout(
        string Text,
        SKFont Font,
        string[] Lines,
        int FontSize,
        float Top,
        float Height,
        SKColor Color);
}
