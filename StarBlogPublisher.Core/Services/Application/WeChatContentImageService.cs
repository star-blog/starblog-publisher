using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Services.Application;

/// <summary>Prepares actual JPEG/PNG bytes for upload without modifying source images.</summary>
internal static class WeChatContentImageService {
    internal const int MaxImageBytes = 1024 * 1024;
    internal sealed record PreparedImage(byte[] Bytes, string Extension);

    internal static async Task<PreparedImage> PrepareAsync(string path) {
        var bytes = await File.ReadAllBytesAsync(path);
        try {
            using var image = DecodedImage.Decode(bytes);
            if (bytes.Length <= MaxImageBytes && image.Format is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) {
                return new PreparedImage(bytes, image.Format == SKEncodedImageFormat.Png ? ".png" : ".jpg");
            }

            var transparent = image.HasTransparentPixel();
            while (true) {
                foreach (var quality in transparent ? new[] { 85 } : new[] { 90, 80, 70, 60 }) {
                    var encoded = transparent
                        ? image.EncodePng()
                        : image.Encode(SKEncodedImageFormat.Jpeg, quality);
                    if (encoded.Length <= MaxImageBytes) {
                        return new PreparedImage(encoded, transparent ? ".png" : ".jpg");
                    }
                }

                if (image.Width == 1 && image.Height == 1) {
                    throw new InvalidOperationException("处理后的正文图片仍超过 1 MB");
                }

                image.Resize(Math.Max(1, image.Width * 3 / 4), Math.Max(1, image.Height * 3 / 4));
            }
        }
        catch (ImageDecodeException ex) when (ex.Kind == ImageDecodeKind.Unrecognized) {
            throw new InvalidOperationException($"无法识别正文图片格式: {Path.GetFileName(path)}", ex);
        }
        catch (ImageDecodeException ex) {
            throw new InvalidOperationException($"正文图片损坏或无法解码: {Path.GetFileName(path)}", ex);
        }
    }
}
