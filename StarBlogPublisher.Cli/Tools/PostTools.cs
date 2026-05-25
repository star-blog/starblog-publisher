using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StarBlogPublisher.Cli.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Tools;

[McpServerToolType]
public static class PostTools {
    [McpServerTool, Description("发布 Markdown 文件为博客文章")]
    public static async Task<string> PostPublish(
        [Description("Markdown 文件的绝对路径")] string filePath,
        [Description("分类 ID")] int categoryId,
        [Description("文章标题（可选，默认使用文件名）")] string? title = null,
        [Description("文章摘要（可选）")] string? summary = null,
        [Description("URL slug（可选）")] string? slug = null,
        [Description("是否直接发布，false 则保存为草稿")] bool publish = true) {

        if (!File.Exists(filePath)) return $"错误: 文件不存在 {filePath}";

        var content = await File.ReadAllTextAsync(filePath);
        var postTitle = title ?? Path.GetFileNameWithoutExtension(filePath);

        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var publishService = new ArticlePublishApplicationService(
            ApiService.Instance, authService, AppSettings.Instance);

        var result = await publishService.PublishAsync(
            filePath, postTitle, content, summary ?? "", categoryId, slug, publish);

        if (result.Success && result.Post != null) {
            var dto = new PublishedPostDto(
                result.Post.Id,
                result.Post.Title,
                result.Post.Slug,
                result.Post.IsPublish ? "已发布" : "草稿");

            return JsonSerializer.Serialize(dto, CliJsonContext.Default.PublishedPostDto);
        }

        return $"错误: {result.ErrorMessage}";
    }

    [McpServerTool, Description("获取文章详情")]
    public static async Task<string> PostGet(
        [Description("文章 ID")] string id) {

        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var publishService = new ArticlePublishApplicationService(
            ApiService.Instance, authService, AppSettings.Instance);
        var result = await publishService.GetPostAsync(id);

        if (result.Success && result.Post != null) {
            var post = result.Post;
            var dto = new PostDetailsDto(
                post.Id,
                post.Title,
                post.Slug,
                post.Category?.Text,
                post.IsPublish ? "已发布" : "草稿",
                post.Summary,
                post.CreationTime,
                post.LastUpdateTime);

            return JsonSerializer.Serialize(dto, CliJsonContext.Default.PostDetailsDto);
        }

        return $"错误: {result.ErrorMessage}";
    }
}
