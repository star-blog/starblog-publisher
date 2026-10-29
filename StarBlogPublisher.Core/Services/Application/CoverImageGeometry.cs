using System;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

public readonly record struct CoverImagePlacement(float X, float Y, float Width, float Height, float PanX, float PanY);
public static class CoverImageGeometry {
    public static CoverImagePlacement Place(CoverDocument d, CoverPixelRect target, int width, int height) {
        var scale = Math.Max((float)target.Width / width, (float)target.Height / height) * (float)d.Zoom;
        var w = Math.Max(target.Width, width * scale); var h = Math.Max(target.Height, height * scale);
        return new(Math.Clamp(target.X + (target.Width - w) / 2 + (float)d.BackgroundX, target.Right - w, target.X),
            Math.Clamp(target.Y + (target.Height - h) / 2 + (float)d.BackgroundY, target.Bottom - h, target.Y), w, h,
            Math.Max(0, (w - target.Width) / 2), Math.Max(0, (h - target.Height) / 2));
    }
}
