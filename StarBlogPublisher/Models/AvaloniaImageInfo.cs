using System;
using Avalonia.Media.Imaging;

namespace StarBlogPublisher.Models;

/// <summary>
/// 图片信息模型（Avalonia 展示版本）
/// 继承 Core 中的无 UI 依赖 ImageInfo，添加 Bitmap 展示能力
/// </summary>
public class AvaloniaImageInfo : ImageInfo {
    public Bitmap? ImageBitmap { get; set; }

    public new static AvaloniaImageInfo Create(string filePath) {
        var baseInfo = ImageInfo.Create(filePath);
        var info = new AvaloniaImageInfo {
            FilePath = baseInfo.FilePath,
            FileName = baseInfo.FileName,
            FileSize = baseInfo.FileSize,
            ImagePath = new Uri(filePath).AbsoluteUri,
            Exists = baseInfo.Exists
        };

        if (info.Exists) {
            try {
                info.ImageBitmap = new Bitmap(filePath);
            }
            catch {
                info.ImageBitmap = null;
            }
        }

        return info;
    }
}
