using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StarBlogPublisher.Cli.Models;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
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

    [McpServerTool, Description("列出站点上的博客文章，支持分页、搜索、分类和发布状态筛选")]
    public static async Task<string> PostList(
        [Description("页码，从 1 开始")] int page = 1,
        [Description("每页条数")] int pageSize = 20,
        [Description("按标题搜索")] string? search = null,
        [Description("分类 ID，0 表示全部")] int categoryId = 0,
        [Description("true 仅已发布，false 仅草稿，省略则全部")] bool? published = null) {

        var result = await CreateLibrary().ListAsync(new PostListQuery {
            Page = page,
            PageSize = pageSize,
            Search = search,
            CategoryId = categoryId,
            IsPublish = published
        });
        if (!result.Success) return $"错误: {result.ErrorMessage}";

        var items = result.Posts.Select(post => new PostListItemDto(
            post.Id,
            post.Title,
            post.Slug,
            post.IsPublish ? "已发布" : "草稿",
            post.Category?.DisplayName,
            post.LastUpdateTime)).ToList();
        var pageDto = new PostListPageDto(result.Pagination.PageNumber, result.Pagination.PageSize,
            result.Pagination.TotalItemCount, result.Pagination.TotalPages, items);
        return JsonSerializer.Serialize(pageDto, CliJsonContext.Default.PostListPageDto);
    }

    [McpServerTool, Description("更新已有文章。可从 Markdown 文件读取正文，ID 可写在同目录 .starblog.json 的 PostId")]
    public static async Task<string> PostUpdate(
        [Description("Markdown 文件的绝对路径")] string filePath,
        [Description("文章 ID，可省略并改从 sidecar 读取")] string? id = null,
        [Description("分类 ID")] int? categoryId = null,
        [Description("文章标题")] string? title = null,
        [Description("文章摘要")] string? summary = null,
        [Description("URL slug")] string? slug = null,
        [Description("是否直接发布，false 则保存为草稿")] bool publish = true) {

        if (!File.Exists(filePath)) return $"错误: 文件不存在 {filePath}";
        var sidecar = await ArticleSidecar.ReadAsync(filePath);
        var postId = id ?? sidecar?.PostId;
        if (string.IsNullOrWhiteSpace(postId)) return "错误: 未提供文章 ID";
        var resolvedCategory = categoryId ?? sidecar?.Category?.Id ?? 0;
        if (resolvedCategory <= 0) return "错误: 请提供分类 ID";

        var result = await CreateLibrary().UpdateAsync(postId, new PostUpdateDto {
            Id = postId,
            Title = title ?? sidecar?.Title ?? Path.GetFileNameWithoutExtension(filePath),
            Summary = summary ?? sidecar?.Description ?? "",
            Slug = slug ?? sidecar?.Slug,
            Content = await File.ReadAllTextAsync(filePath),
            CategoryId = resolvedCategory,
            IsPublish = publish
        });
        if (!result.Success || result.Post == null) return $"错误: {result.ErrorMessage}";
        return JsonSerializer.Serialize(
            new PublishedPostDto(result.Post.Id, result.Post.Title, result.Post.Slug,
                result.Post.IsPublish ? "已发布" : "草稿"),
            CliJsonContext.Default.PublishedPostDto);
    }

    [McpServerTool, Description("删除一篇博客文章")]
    public static async Task<string> PostDelete([Description("文章 ID")] string id) {
        var result = await CreateLibrary().DeleteAsync(id);
        return result.Success ? result.Message ?? $"已删除 {id}" : $"错误: {result.ErrorMessage}";
    }

    [McpServerTool, Description("把线上文章下载为本地 Markdown 和 .starblog.json")]
    public static async Task<string> PostPull(
        [Description("文章 ID")] string id,
        [Description("保存的 Markdown 绝对路径")] string outputPath) {

        var result = await CreateLibrary().PullAsync(id, outputPath);
        return result.Success ? $"已保存 {outputPath}" : $"错误: {result.ErrorMessage}";
    }

    private static ArticleLibraryApplicationService CreateLibrary() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        return new ArticleLibraryApplicationService(ApiService.Instance, authService);
    }
}
