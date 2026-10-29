using System;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Services.Application;

/// <summary>Bytes-in, bytes-out helpers for the cover studio. Skia stays inside Core.</summary>
public static class ImagePreview {
    public static byte[] CreatePngThumbnail(byte[] bytes, int maxWidth, int maxHeight) {
        using var image = DecodedImage.Decode(bytes);
        FitWithin(image, maxWidth, maxHeight);
        return image.EncodePng();
    }

    public static void EnsureDecodable(byte[] bytes) {
        using var image = DecodedImage.Decode(bytes);
    }

    private static void FitWithin(DecodedImage image, int maxWidth, int maxHeight) {
        if (image.Width <= maxWidth && image.Height <= maxHeight) return;
        var scale = Math.Min(maxWidth / (double)image.Width, maxHeight / (double)image.Height);
        image.Resize(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
    }
}
