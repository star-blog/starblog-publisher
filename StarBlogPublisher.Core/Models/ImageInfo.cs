using System;
using System.IO;

namespace StarBlogPublisher.Models;

/// <summary>
/// 图片信息模型（无 UI 依赖版本）
/// </summary>
public class ImageInfo {
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public bool Exists { get; set; }

    public static ImageInfo Create(string filePath) {
        var info = new ImageInfo {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            ImagePath = filePath,
            Exists = File.Exists(filePath)
        };

        if (info.Exists) {
            try {
                var fileInfo = new FileInfo(filePath);
                info.FileSize = FormatFileSize(fileInfo.Length);
            }
            catch {
                info.FileSize = "未知大小";
            }
        }
        else {
            info.FileSize = "文件不存在";
        }

        return info;
    }

    private static string FormatFileSize(long bytes) {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
