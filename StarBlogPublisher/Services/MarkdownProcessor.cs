using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeLab.Share.Extensions;
using Markdig;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Refit;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

public class MarkdownProcessor(string filepath, BlogPost post) {
    public event Action<int, int>? ImageUploadProgress;

    private ImageCompressionService? _compressionService;
    /// <summary>
    /// Markdown内容解析，上传图片到后端 & 替换图片链接为后端URL
    /// </summary>
    /// <param name="enableCompression">是否启用图片压缩</param>
    /// <param name="compressionConfig">图片压缩配置</param>
    /// <returns></returns>
    public async Task<string> MarkdownParse(bool enableCompression = true, ImageCompressionConfig? compressionConfig = null) {
        if (post.Content == null) {
            return string.Empty;
        }

        // 初始化图片压缩服务
        if (enableCompression) {
            _compressionService = new ImageCompressionService(compressionConfig);
        }

        try {
            // 先统计需要上传的图片总数
            var document = Markdig.Markdown.Parse(post.Content);
            var totalImages = 0;
            var uploadedImages = 0;

            foreach (var node in document.AsEnumerable()) {
                if (node is not ParagraphBlock { Inline: { } } paragraphBlock) continue;
                foreach (var inline in paragraphBlock.Inline) {
                    if (inline is not LinkInline { IsImage: true } linkInline) continue;
                    if (string.IsNullOrWhiteSpace(linkInline.Url)) continue;
                    var imgUrl = Uri.UnescapeDataString(linkInline.Url);
                    if (!imgUrl.StartsWith("http")) totalImages++;
                }
            }

            // 重新解析文档处理图片上传
            document = Markdig.Markdown.Parse(post.Content);

            foreach (var node in document.AsEnumerable()) {
                if (node is not ParagraphBlock { Inline: { } } paragraphBlock) continue;
                foreach (var inline in paragraphBlock.Inline) {
                    if (inline is not LinkInline { IsImage: true } linkInline) continue;

                    if (string.IsNullOrWhiteSpace(linkInline.Url)) continue;
                    var imgUrl = Uri.UnescapeDataString(linkInline.Url);
                    if (imgUrl.StartsWith("http")) continue;

                    // 规范化路径
                    imgUrl = imgUrl.Replace('/', Path.DirectorySeparatorChar) // 统一路径分隔符
                        .Replace(".\\", "") // 移除相对路径前缀
                        .Replace("./", ""); // 移除相对路径前缀
                    var baseDir = Path.GetDirectoryName(filepath) ?? "";
                    imgUrl = Path.GetFullPath(Path.Combine(baseDir, imgUrl));

                    try {
                        string actualImagePath = imgUrl;
                        string actualImageFilename = Path.GetFileName(imgUrl);

                        // 如果启用了压缩，先压缩图片
                        if (enableCompression && _compressionService != null) {
                            var compressionResult = await _compressionService.CompressImageAsync(imgUrl);
                            if (compressionResult.Success) {
                                actualImagePath = compressionResult.CompressedPath;
                                actualImageFilename = Path.GetFileName(actualImagePath);
                                Console.WriteLine($"图片压缩成功: {imgUrl} -> {actualImagePath}, 压缩率: {compressionResult.CompressionRatio:P2}");
                            }
                            else {
                                Console.WriteLine($"图片压缩失败，使用原图: {imgUrl}");
                            }
                        }

                        // 上传图片到后端（使用压缩后的图片或原图）
                        await using var fileStream = File.OpenRead(actualImagePath);
                        var streamPart = new StreamPart(fileStream, actualImageFilename);
                        var response = await ApiService.Instance.BlogPost.UploadImage(post.Id, streamPart);

                        if (response is { Successful: true, Data: not null }) {
                            // 替换图片链接为后端返回的URL
                            linkInline.Url = response.Data.ImgUrl;
                            Console.WriteLine($"上传图片 {actualImageFilename} 成功，URL: {response.Data.ImgUrl}");
                            uploadedImages++;
                            ImageUploadProgress?.Invoke(uploadedImages, totalImages);
                        }
                        else {
                            // 上传失败，保留原始链接
                            Console.WriteLine($"上传图片 {actualImageFilename} 失败: {response.Message}");
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"处理图片 {imgUrl} 异常: {ex.Message}");
                    }
                }
            }

            await using var writer = new StringWriter();
            var render = new NormalizeRenderer(writer);
            render.Render(document);
            return writer.ToString();
        }
        finally {
            // 清理压缩服务和临时文件
            _compressionService?.Dispose();
            _compressionService = null;
        }
    }

    /// <summary>
    /// 从文章正文提取前 <paramref name="length"/> 字的梗概
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public string GetSummary(int length) {
        return post.Content == null
            ? string.Empty
            : Markdig.Markdown.ToPlainText(post.Content).Limit(length);
    }
    
    /// <summary>
    /// 从Markdown内容中提取所有图片路径
    /// </summary>
    /// <returns>图片路径数组</returns>
    public string[] ExtractImagePaths() {
        if (post.Content == null) {
            return Array.Empty<string>();
        }

        var document = Markdig.Markdown.Parse(post.Content);
        var imagePaths = new List<string>();
        var baseDir = Path.GetDirectoryName(filepath) ?? "";

        foreach (var node in document.AsEnumerable()) {
            if (node is not ParagraphBlock { Inline: { } } paragraphBlock) continue;
            foreach (var inline in paragraphBlock.Inline) {
                if (inline is not LinkInline { IsImage: true } linkInline) continue;
                if (string.IsNullOrWhiteSpace(linkInline.Url)) continue;
                
                var imgUrl = Uri.UnescapeDataString(linkInline.Url);
                
                // 如果是本地图片路径，转换为绝对路径
                if (!imgUrl.StartsWith("http")) {
                    // 规范化路径
                    imgUrl = imgUrl.Replace('/', Path.DirectorySeparatorChar) // 统一路径分隔符
                        .Replace(".\\\\", "") // 移除相对路径前缀
                        .Replace("./", ""); // 移除相对路径前缀
                    
                    imgUrl = Path.GetFullPath(Path.Combine(baseDir, imgUrl));
                    imagePaths.Add(imgUrl);
                }
            }
        }

        // 添加自定义解析逻辑处理带空格的图片路径
        // Markdig 无法正确解析带空格的图片路径，需要手动处理
        var customPaths = ExtractImagePathsWithSpaces(post.Content, baseDir);
        imagePaths.AddRange(customPaths);
        
        // 去重并返回
        return imagePaths.Distinct().ToArray();
    }
    
    /// <summary>
    /// 提取带空格的图片路径（Markdig 无法正确解析的情况）
    /// </summary>
    /// <param name="content">Markdown 内容</param>
    /// <param name="baseDir">基础目录</param>
    /// <returns>图片路径列表</returns>
    private List<string> ExtractImagePathsWithSpaces(string content, string baseDir) {
        var imagePaths = new List<string>();
        
        // 使用正则表达式匹配图片语法：![alt](path)
        // 支持路径中包含空格、中文字符等
        var imagePattern = @"!\[([^\]]*)\]\(([^)]+)\)";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, imagePattern);
        
        foreach (System.Text.RegularExpressions.Match match in matches) {
            if (match.Groups.Count >= 3) {
                var imagePath = match.Groups[2].Value.Trim();
                
                // 移除可能的引号
                if ((imagePath.StartsWith('"') && imagePath.EndsWith('"')) ||
                    (imagePath.StartsWith('\'') && imagePath.EndsWith('\''))) {
                    imagePath = imagePath.Substring(1, imagePath.Length - 2);
                }
                
                // URL 解码
                imagePath = Uri.UnescapeDataString(imagePath);
                
                // 只处理本地路径
                if (!imagePath.StartsWith("http")) {
                    // 规范化路径
                    imagePath = imagePath.Replace('/', Path.DirectorySeparatorChar)
                         .Replace(".\\\\", "")
                         .Replace("./", "");
                    
                    var fullPath = Path.GetFullPath(Path.Combine(baseDir, imagePath));
                    imagePaths.Add(fullPath);
                }
            }
        }
        
        return imagePaths;
    }
}