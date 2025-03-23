using System;
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
    /// <summary>
    /// Markdown内容解析，上传图片到后端 & 替换图片链接为后端URL
    /// </summary>
    /// <returns></returns>
    public async Task<string> MarkdownParse() {
        if (post.Content == null) {
            return string.Empty;
        }

        var document = Markdig.Markdown.Parse(post.Content);

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

                // 获取图片文件名
                var imgFilename = Path.GetFileName(imgUrl);

                try {
                    // 直接从原始路径读取图片并上传到后端
                    await using var fileStream = File.OpenRead(imgUrl);
                    var streamPart = new StreamPart(fileStream, imgFilename);
                    var response = await ApiService.Instance.BlogPost.UploadImage(post.Id, streamPart);

                    if (response is { Successful: true, Data: not null }) {
                        // 替换图片链接为后端返回的URL
                        linkInline.Url = response.Data.ImgUrl;
                        Console.WriteLine($"上传图片 {imgUrl} 成功，URL: {response.Data.ImgUrl}");
                    }
                    else {
                        // 上传失败，保留原始链接
                        Console.WriteLine($"上传图片 {imgUrl} 失败: {response.Message}");
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"上传图片 {imgUrl} 异常: {ex.Message}");
                }
            }
        }


        await using var writer = new StringWriter();
        var render = new NormalizeRenderer(writer);
        render.Render(document);
        return writer.ToString();
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
}