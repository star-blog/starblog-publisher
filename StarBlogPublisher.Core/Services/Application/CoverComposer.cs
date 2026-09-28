using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
    public RenderedCover(Image<Rgba32> image, int effectiveFontSize, string fontFamily) {
        Image = image;
        EffectiveFontSize = effectiveFontSize;
        FontFamily = fontFamily;
    }

    public Image<Rgba32> Image { get; }
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
    private static readonly Color FallbackColor = Color.ParseHex("1F2933");
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

    public async Task<ComposedCover> ComposeAsync(Stream? background, CoverComposition composition, CancellationToken cancellationToken = default) {
        using var rendered = await RenderAsync(background, composition, cancellationToken);
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StarBlogPublisher", "cover-studio");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, $"{Guid.NewGuid():N}.jpg");
        await SaveJpegAsync(rendered.Image, path, cancellationToken);
        return new ComposedCover(path, CanvasWidth, CanvasHeight, rendered.EffectiveFontSize, rendered.FontFamily);
    }

    public async Task<RenderedCover> RenderAsync(Stream? background, CoverComposition composition, CancellationToken cancellationToken = default) {
        var canvas = new Image<Rgba32>(CanvasWidth, CanvasHeight, FallbackColor);
        try {
            if (background != null) {
                await DrawBackgroundAsync(canvas, background, cancellationToken);
            }

            var title = NormalizeTitle(composition.Title);
            var effectiveSize = 0;
            var fontFamily = "";
            if (title.Length > 0) {
                if (!TryResolveFont(out var family, out fontFamily)) {
                    throw new InvalidOperationException("找不到可用的中文字体。Windows 需要微软雅黑，macOS 需要苹方，Linux 需要 Noto Sans CJK。");
                }

                var layout = LayoutTitle(family, title, composition);
                effectiveSize = layout.FontSize;
                canvas.Mutate(ctx => DrawTitle(ctx, layout, composition));
            }

            return new RenderedCover(canvas, effectiveSize, fontFamily);
        }
        catch {
            canvas.Dispose();
            throw;
        }
    }

    public byte[] CreatePreviewPng(string composedPath, CoverPreviewFrame frame) {
        using var image = Image.Load<Rgba32>(composedPath);
        var crop = frame switch {
            CoverPreviewFrame.Headline => CoverSafeZone.Headline(image.Width, image.Height),
            CoverPreviewFrame.Secondary => CoverSafeZone.Secondary(image.Width, image.Height),
            _ => new CoverPixelRect(0, 0, image.Width, image.Height)
        };
        using var framed = image.Clone(ctx => ctx.Crop(new Rectangle(crop.X, crop.Y, crop.Width, crop.Height)));
        using var stream = new MemoryStream();
        framed.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    internal static bool TryResolveFont(out FontFamily family, out string familyName) {
        family = default;
        familyName = "";
        try {
            foreach (var name in PreferredFontFamilies) {
                if (SystemFonts.TryGet(name, out family)) {
                    familyName = family.Name;
                    return true;
                }
            }

            foreach (var candidate in SystemFonts.Families) {
                var name = candidate.Name;
                if (name.Contains("YaHei", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("PingFang", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Noto Sans CJK", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Noto Sans SC", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Source Han Sans", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("WenQuanYi", StringComparison.OrdinalIgnoreCase)) {
                    family = candidate;
                    familyName = name;
                    return true;
                }
            }
        }
        catch (Exception) {
            return false;
        }

        return false;
    }

    private static async Task DrawBackgroundAsync(Image<Rgba32> canvas, Stream background, CancellationToken cancellationToken) {
        Image<Rgba32> source;
        try {
            source = await Image.LoadAsync<Rgba32>(background, cancellationToken);
        }
        catch (UnknownImageFormatException ex) {
            throw new InvalidOperationException("无法读取背景图片", ex);
        }
        catch (InvalidImageContentException ex) {
            throw new InvalidOperationException("无法读取背景图片", ex);
        }

        using (source) {
            source.Mutate(ctx => ctx
                .AutoOrient()
                .Resize(new ResizeOptions {
                    Size = new Size(CanvasWidth, CanvasHeight),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));
            canvas.Mutate(ctx => ctx.DrawImage(source, new Point(0, 0), 1f));
        }
    }

    private static TitleLayout LayoutTitle(FontFamily family, string title, CoverComposition composition) {
        var textArea = CoverSafeZone.TextArea();
        var requested = Math.Clamp(composition.FontSize, MinFontSize, MaxFontSize);
        var size = requested;
        FontRectangle measured = default;
        Font font = default!;
        while (true) {
            font = CreateFont(family, size, composition.Bold);
            measured = TextMeasurer.MeasureAdvance(title, MeasureOptions(font, textArea.Width));
            if (measured.Height <= textArea.Height + 0.5f || size <= MinFontSize) break;
            size -= 2;
        }

        var blockHeight = Math.Max(1f, measured.Height);
        var y = composition.Placement switch {
            CoverTextVerticalPlacement.Top => textArea.Y,
            CoverTextVerticalPlacement.Center => textArea.Y + (textArea.Height - blockHeight) / 2f,
            _ => textArea.Y + textArea.Height - blockHeight
        };
        var centered = composition.Align == CoverTextHorizontalAlign.Center;
        var options = new RichTextOptions(font) {
            WrappingLength = textArea.Width,
            HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Origin = new PointF(centered ? textArea.X + textArea.Width / 2f : textArea.X, y),
            Dpi = 72
        };
        return new TitleLayout(title, font, size, options, ResolveColor(composition), y, blockHeight);
    }

    private static void DrawTitle(IImageProcessingContext ctx, TitleLayout layout, CoverComposition composition) {
        if (composition.Scrim) DrawScrim(ctx, composition.Placement, layout.Top, layout.Height);
        var clip = CoverSafeZone.Intersection();
        ctx.Clip(new RectangularPolygon(clip.X, clip.Y, clip.Width, clip.Height), textCtx => {
            textCtx.DrawText(layout.Options, layout.Text, layout.Color);
        });
    }

    private static void DrawScrim(IImageProcessingContext ctx, CoverTextVerticalPlacement placement, float textTop, float textHeight) {
        const byte alpha = 176;
        var dark = Color.FromRgba(0, 0, 0, alpha);
        var clear = Color.Transparent;
        float top;
        float bottom;
        ColorStop[] stops;
        switch (placement) {
            case CoverTextVerticalPlacement.Top:
                top = 0;
                bottom = Math.Min(CanvasHeight, textTop + textHeight + 80);
                stops = [new ColorStop(0, dark), new ColorStop(1, clear)];
                break;
            case CoverTextVerticalPlacement.Center:
                top = Math.Max(0, textTop - 48);
                bottom = Math.Min(CanvasHeight, textTop + textHeight + 48);
                stops = [
                    new ColorStop(0, clear),
                    new ColorStop(0.35f, dark),
                    new ColorStop(0.65f, dark),
                    new ColorStop(1, clear)
                ];
                break;
            default:
                top = Math.Max(0, textTop - 100);
                bottom = CanvasHeight;
                stops = [new ColorStop(0, clear), new ColorStop(1, dark)];
                break;
        }

        if (bottom - top < 1) return;
        var brush = new LinearGradientBrush(
            new PointF(0, top),
            new PointF(0, bottom),
            GradientRepetitionMode.None,
            stops);
        ctx.Fill(brush, new RectangleF(0, top, CanvasWidth, bottom - top));
    }

    private static RichTextOptions MeasureOptions(Font font, int wrappingWidth) => new(font) {
        WrappingLength = wrappingWidth,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Dpi = 72
    };

    private static Font CreateFont(FontFamily family, float size, bool bold) {
        var styles = family.GetAvailableStyles().ToArray();
        var style = bold && styles.Contains(FontStyle.Bold)
            ? FontStyle.Bold
            : styles.Contains(FontStyle.Regular)
                ? FontStyle.Regular
                : styles.FirstOrDefault();
        return family.CreateFont(size, style);
    }

    private static Color ResolveColor(CoverComposition composition) {
        if (composition.Color == CoverTextColor.Black) return Color.Black;
        if (composition.Color == CoverTextColor.Custom && TryParseHex(composition.CustomColorHex, out var custom)) {
            return custom;
        }

        return Color.White;
    }

    private static bool TryParseHex(string hex, out Color color) {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var text = hex.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        return Color.TryParseHex(text, out color);
    }

    private static string NormalizeTitle(string? title) {
        if (string.IsNullOrWhiteSpace(title)) return "";
        return title.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    }

    private static async Task SaveJpegAsync(Image image, string path, CancellationToken cancellationToken) {
        var quality = 90;
        while (true) {
            await image.SaveAsJpegAsync(path, new JpegEncoder { Quality = quality }, cancellationToken);
            if (new FileInfo(path).Length <= MaxJpegBytes || quality <= 60) return;
            quality -= 10;
        }
    }

    private readonly record struct TitleLayout(
        string Text,
        Font Font,
        int FontSize,
        RichTextOptions Options,
        Color Color,
        float Top,
        float Height);
}
