using System;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// Pixel window shared by the WeChat headline and secondary center crops.
/// Cover text is placed inside this window so both crops keep the title.
/// </summary>
public static class CoverSafeZone {
    public const int CanvasWidth = 1200;
    public const int CanvasHeight = 900;
    public const int Padding = 48;
    public const double HeadlineAspect = 2.35;

    public static CoverPixelRect Headline(int width = CanvasWidth, int height = CanvasHeight) =>
        Center(width, height, HeadlineAspect);

    public static CoverPixelRect Secondary(int width = CanvasWidth, int height = CanvasHeight) =>
        Center(width, height, 1d);

    public static CoverPixelRect Intersection(int width = CanvasWidth, int height = CanvasHeight) =>
        Intersect(Headline(width, height), Secondary(width, height));

    public static CoverPixelRect TextArea(int width = CanvasWidth, int height = CanvasHeight) =>
        Inset(Intersection(width, height), Padding);

    public static CoverPixelRect Intersect(CoverPixelRect a, CoverPixelRect b) {
        var x1 = Math.Max(a.X, b.X);
        var y1 = Math.Max(a.Y, b.Y);
        var x2 = Math.Min(a.Right, b.Right);
        var y2 = Math.Min(a.Bottom, b.Bottom);
        return new CoverPixelRect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }

    public static CoverPixelRect Inset(CoverPixelRect rect, int padding) {
        var pad = Math.Max(0, padding);
        var width = rect.Width - pad * 2;
        var height = rect.Height - pad * 2;
        if (width < 1 || height < 1) return rect;
        return new CoverPixelRect(rect.X + pad, rect.Y + pad, width, height);
    }

    private static CoverPixelRect Center(int width, int height, double aspect) {
        if (width <= 0 || height <= 0) return default;

        var imageAspect = (double)width / height;
        double x, y, w, h;
        if (imageAspect > aspect) {
            w = height * aspect;
            h = height;
            x = (width - w) / 2d;
            y = 0;
        }
        else {
            w = width;
            h = width / aspect;
            x = 0;
            y = (height - h) / 2d;
        }

        var left = (int)Math.Floor(x);
        var top = (int)Math.Floor(y);
        var right = (int)Math.Ceiling(x + w);
        var bottom = (int)Math.Ceiling(y + h);
        left = Math.Clamp(left, 0, Math.Max(0, width - 1));
        top = Math.Clamp(top, 0, Math.Max(0, height - 1));
        right = Math.Clamp(right, left + 1, width);
        bottom = Math.Clamp(bottom, top + 1, height);
        return new CoverPixelRect(left, top, right - left, bottom - top);
    }
}

public readonly record struct CoverPixelRect(int X, int Y, int Width, int Height) {
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
