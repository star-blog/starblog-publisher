using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace StarBlogPublisher.Models;

/// <summary>
/// 图片信息模型
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// 图片文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 图片文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 图片文件大小（格式化后的字符串）
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// 图片路径（用于显示）
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>
    /// 图片位图对象（用于Avalonia显示）
    /// </summary>
    public Bitmap? ImageBitmap { get; set; }

    /// <summary>
    /// 图片是否存在
    /// </summary>
    public bool Exists { get; set; }

    /// <summary>
    /// 创建图片信息实例
    /// </summary>
    /// <param name="filePath">图片文件路径</param>
    /// <returns>图片信息实例</returns>
    public static ImageInfo Create(string filePath)
    {
        var imageInfo = new ImageInfo
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            // 将本地文件路径转换为file:// URI格式，以便Avalonia Image控件正确加载
            ImagePath = new Uri(filePath).AbsoluteUri,
            Exists = File.Exists(filePath)
        };

        // 尝试加载图片为Bitmap对象
        if (imageInfo.Exists)
        {
            try
            {
                imageInfo.ImageBitmap = new Bitmap(filePath);
            }
            catch
            {
                // 如果加载失败，ImageBitmap保持为null
                imageInfo.ImageBitmap = null;
            }
        }

        if (imageInfo.Exists)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                imageInfo.FileSize = FormatFileSize(fileInfo.Length);
            }
            catch
            {
                imageInfo.FileSize = "未知大小";
            }
        }
        else
        {
            imageInfo.FileSize = "文件不存在";
        }

        return imageInfo;
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化后的文件大小字符串</returns>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}