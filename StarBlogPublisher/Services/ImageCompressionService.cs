using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;

namespace StarBlogPublisher.Services;

/// <summary>
/// 图片压缩配置
/// </summary>
public record ImageCompressionConfig(
    int MaxWidth = 1200,
    int MaxHeight = 800,
    int Quality = 85,
    bool EnableResize = true,
    bool PreferWebP = true
);

/// <summary>
/// 图片压缩结果
/// </summary>
public record ImageCompressionResult(
    bool Success,
    string OriginalPath,
    string CompressedPath,
    long OriginalSize,
    long CompressedSize,
    double CompressionRatio
);

/// <summary>
/// 轻量级图片压缩服务
/// 专为文章发布流程设计，提供临时压缩和路径映射功能
/// </summary>
public class ImageCompressionService : IDisposable
{
    private readonly ImageCompressionConfig _config;
    private readonly string _tempDirectory;
    private readonly Dictionary<string, string> _pathMappings;
    private bool _disposed;

    public ImageCompressionService(ImageCompressionConfig? config = null)
    {
        _config = config ?? new ImageCompressionConfig();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "CompressedImages", Guid.NewGuid().ToString("N")[..8]);
        _pathMappings = new Dictionary<string, string>();
        
        // 确保临时目录存在
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// 获取原始路径与压缩后路径的映射关系
    /// </summary>
    public IReadOnlyDictionary<string, string> PathMappings => _pathMappings;

    /// <summary>
    /// 获取临时目录路径
    /// </summary>
    public string TempDirectory => _tempDirectory;

    /// <summary>
    /// 压缩单个图片
    /// </summary>
    /// <param name="imagePath">原始图片路径</param>
    /// <returns>压缩结果</returns>
    public async Task<ImageCompressionResult> CompressImageAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return new ImageCompressionResult(false, imagePath, string.Empty, 0, 0, 0);
        }

        if (!IsImageFile(imagePath))
        {
            return new ImageCompressionResult(false, imagePath, string.Empty, 0, 0, 0);
        }

        try
        {
            var originalInfo = new FileInfo(imagePath);
            var originalSize = originalInfo.Length;

            // 生成压缩后的文件路径
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var extension = Path.GetExtension(imagePath).ToLower();
            
            using var image = await Image.LoadAsync(imagePath);
            
            // 应用尺寸调整
            if (_config.EnableResize && ShouldResize(image))
            {
                ApplyResize(image);
            }

            // 选择最优输出格式和路径
            var (outputPath, outputFormat) = GetOptimalOutputPath(fileName, extension, image);
            
            // 保存压缩后的图片
            await SaveCompressedImageAsync(image, outputPath, outputFormat);

            var compressedInfo = new FileInfo(outputPath);
            var compressedSize = compressedInfo.Length;
            var compressionRatio = originalSize > 0 ? 1.0 - (double)compressedSize / originalSize : 0;

            // 如果压缩后文件更大，使用原文件
            if (compressedSize >= originalSize)
            {
                File.Delete(outputPath);
                outputPath = Path.Combine(_tempDirectory, Path.GetFileName(imagePath));
                File.Copy(imagePath, outputPath, true);
                compressedSize = originalSize;
                compressionRatio = 0;
            }

            // 记录路径映射
            _pathMappings[imagePath] = outputPath;

            return new ImageCompressionResult(true, imagePath, outputPath, originalSize, compressedSize, compressionRatio);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"压缩图片失败 {imagePath}: {ex.Message}");
            return new ImageCompressionResult(false, imagePath, string.Empty, 0, 0, 0);
        }
    }

    /// <summary>
    /// 批量压缩图片
    /// </summary>
    /// <param name="imagePaths">图片路径列表</param>
    /// <returns>压缩结果列表</returns>
    public async Task<List<ImageCompressionResult>> CompressImagesAsync(IEnumerable<string> imagePaths)
    {
        var results = new List<ImageCompressionResult>();
        
        foreach (var imagePath in imagePaths)
        {
            var result = await CompressImageAsync(imagePath);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// 根据原始路径获取压缩后的路径
    /// </summary>
    /// <param name="originalPath">原始路径</param>
    /// <returns>压缩后的路径，如果没有找到则返回原始路径</returns>
    public string GetCompressedPath(string originalPath)
    {
        return _pathMappings.TryGetValue(originalPath, out var compressedPath) ? compressedPath : originalPath;
    }

    /// <summary>
    /// 检查是否为图片文件
    /// </summary>
    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }

    /// <summary>
    /// 判断是否需要调整尺寸
    /// </summary>
    private bool ShouldResize(Image image)
    {
        return image.Width > _config.MaxWidth || image.Height > _config.MaxHeight;
    }

    /// <summary>
    /// 应用尺寸调整
    /// </summary>
    private void ApplyResize(Image image)
    {
        var scaleX = (double)_config.MaxWidth / image.Width;
        var scaleY = (double)_config.MaxHeight / image.Height;
        var scale = Math.Min(scaleX, scaleY);

        if (scale >= 1.0) return;

        var newWidth = (int)(image.Width * scale);
        var newHeight = (int)(image.Height * scale);

        image.Mutate(x => x.Resize(newWidth, newHeight));
    }

    /// <summary>
    /// 获取最优输出路径和格式
    /// </summary>
    private (string outputPath, string format) GetOptimalOutputPath(string fileName, string originalExtension, Image image)
    {
        // GIF 保持原格式
        if (originalExtension == ".gif")
        {
            return (Path.Combine(_tempDirectory, $"{fileName}.gif"), "gif");
        }

        // 根据配置和图片特征选择格式
        if (_config.PreferWebP && !HasTransparency(image))
        {
            return (Path.Combine(_tempDirectory, $"{fileName}.webp"), "webp");
        }

        // 有透明度的图片使用 WebP 或保持 PNG
        if (HasTransparency(image))
        {
            return _config.PreferWebP 
                ? (Path.Combine(_tempDirectory, $"{fileName}.webp"), "webp")
                : (Path.Combine(_tempDirectory, $"{fileName}.png"), "png");
        }

        // 其他情况使用 JPEG
        return (Path.Combine(_tempDirectory, $"{fileName}.jpg"), "jpeg");
    }

    /// <summary>
    /// 保存压缩后的图片
    /// </summary>
    private async Task SaveCompressedImageAsync(Image image, string outputPath, string format)
    {
        switch (format.ToLower())
        {
            case "webp":
                var webpEncoder = new WebpEncoder { Quality = _config.Quality };
                await image.SaveAsWebpAsync(outputPath, webpEncoder);
                break;
            case "jpeg":
            case "jpg":
                var jpegEncoder = new JpegEncoder { Quality = _config.Quality };
                await image.SaveAsJpegAsync(outputPath, jpegEncoder);
                break;
            case "gif":
                await image.SaveAsGifAsync(outputPath);
                break;
            default:
                await image.SaveAsPngAsync(outputPath);
                break;
        }
    }

    /// <summary>
    /// 检测图片是否有透明度
    /// </summary>
    private static bool HasTransparency(Image image)
    {
        return image.PixelType.BitsPerPixel == 32 || image.PixelType.ToString().Contains("Rgba");
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"清理临时目录失败: {ex.Message}");
        }

        _disposed = true;
    }
}
