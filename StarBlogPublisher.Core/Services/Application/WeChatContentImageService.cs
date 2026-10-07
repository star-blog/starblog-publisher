using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace StarBlogPublisher.Services.Application;

/// <summary>Prepares actual JPEG/PNG bytes for upload without modifying source images.</summary>
internal static class WeChatContentImageService {
    internal const int MaxImageBytes = 1024 * 1024;
    internal sealed record PreparedImage(byte[] Bytes, string Extension);

    internal static async Task<PreparedImage> PrepareAsync(string path) {
        var bytes = await File.ReadAllBytesAsync(path);
        try {
            using var image = Image.Load<Rgba32>(bytes);
            var format = image.Metadata.DecodedImageFormat?.Name;
            if (bytes.Length <= MaxImageBytes && format is "JPEG" or "PNG") {
                return new PreparedImage(bytes, format == "PNG" ? ".png" : ".jpg");
            }

            // Animated formats become a static first frame.
            while (image.Frames.Count > 1) image.Frames.RemoveFrame(image.Frames.Count - 1);
            image.Mutate(context => context.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;
            var transparent = false;
            image.ProcessPixelRows(accessor => {
                for (var y = 0; y < accessor.Height && !transparent; y++) {
                    foreach (var pixel in accessor.GetRowSpan(y)) {
                        if (pixel.A < 255) {
                            transparent = true;
                            break;
                        }
                    }
                }
            });

            while (true) {
                foreach (var quality in transparent ? new[] { 85 } : new[] { 90, 80, 70, 60 }) {
                    using var output = new MemoryStream();
                    if (transparent) await image.SaveAsPngAsync(output);
                    else await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = quality });
                    if (output.Length <= MaxImageBytes) {
                        return new PreparedImage(output.ToArray(), transparent ? ".png" : ".jpg");
                    }
                }

                if (image.Width == 1 && image.Height == 1) {
                    throw new InvalidOperationException("处理后的正文图片仍超过 1 MB");
                }
                image.Mutate(context => context.Resize(
                    Math.Max(1, image.Width * 3 / 4), Math.Max(1, image.Height * 3 / 4)));
            }
        }
        catch (UnknownImageFormatException ex) {
            throw new InvalidOperationException($"无法识别正文图片格式: {Path.GetFileName(path)}", ex);
        }
        catch (InvalidImageContentException ex) {
            throw new InvalidOperationException($"正文图片损坏或无法解码: {Path.GetFileName(path)}", ex);
        }
    }
}
