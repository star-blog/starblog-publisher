using System;
using System.Threading.Tasks;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 文章发布应用服务
/// 封装文章发布、图片处理等业务流程
/// </summary>
public class ArticlePublishApplicationService {
    private readonly ApiService _api;
    private readonly AuthApplicationService _authService;
    private readonly AppSettings _settings;

    public ArticlePublishApplicationService(ApiService api, AuthApplicationService authService, AppSettings settings) {
        _api = api;
        _authService = authService;
        _settings = settings;
    }

    /// <summary>
    /// 发布文章
    /// </summary>
    /// <param name="filePath">Markdown 文件路径</param>
    /// <param name="title">文章标题</param>
    /// <param name="content">文章内容</param>
    /// <param name="summary">文章摘要</param>
    /// <param name="categoryId">分类 ID</param>
    /// <param name="slug">URL slug（可选）</param>
    /// <param name="publish">是否直接发布（true=发布，false=草稿）</param>
    /// <param name="onProgress">进度回调 (step, message)</param>
    /// <returns>发布结果</returns>
    public async Task<PublishResult> PublishAsync(
        string filePath,
        string title,
        string content,
        string summary,
        int categoryId,
        string? slug = null,
        bool publish = true,
        Action<int, string>? onProgress = null) {

        // 验证前置条件
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return PublishResult.Fail(authCheck.ErrorMessage!);

        if (string.IsNullOrWhiteSpace(content)) return PublishResult.Fail("文章内容为空");
        if (categoryId <= 0) return PublishResult.Fail("请选择文章分类");

        try {
            // 第一步：创建文章
            onProgress?.Invoke(10, "正在创建文章...");
            var createResp = await _api.BlogPost.Add(new PostCreationDto {
                Title = title,
                Content = content,
                Summary = summary,
                CategoryId = categoryId,
                Slug = string.IsNullOrWhiteSpace(slug) ? null : slug
            });

            if (createResp?.Data == null) {
                return PublishResult.Fail($"创建文章失败: {createResp?.Message ?? "未知错误"}");
            }

            var blogPost = createResp.Data;
            onProgress?.Invoke(30, "文章已创建，正在处理图片...");

            // 第二步：处理 Markdown 中的图片
            var processor = new MarkdownProcessor(filePath, blogPost);
            processor.ImageUploadProgress += (uploaded, total) => {
                var progress = 30 + (int)(uploaded * 50.0 / total);
                onProgress?.Invoke(progress, $"正在上传图片 ({uploaded}/{total})...");
            };

            var compressionConfig = new ImageCompressionConfig(
                MaxWidth: 1200,
                MaxHeight: 800,
                Quality: 85,
                PreferWebP: false
            );

            var processedContent = await processor.MarkdownParse(true, compressionConfig);
            onProgress?.Invoke(80, "图片处理完成");

            // 第三步：如果内容有变化或需要修正发布状态，更新文章
            var needsContentUpdate = processedContent != content;
            var needsStatusUpdate = blogPost.IsPublish != publish;

            if (needsContentUpdate || needsStatusUpdate) {
                onProgress?.Invoke(85, needsContentUpdate ? "正在更新文章内容..." : "正在更新发布状态...");
                var updateDto = new PostUpdateDto {
                    Id = blogPost.Id,
                    Title = title,
                    Content = processedContent,
                    Summary = summary,
                    CategoryId = categoryId,
                    IsPublish = publish,
                    Slug = string.IsNullOrWhiteSpace(slug) ? blogPost.Slug : slug,
                    Status = blogPost.Status
                };

                var updateResp = await _api.BlogPost.Update(blogPost.Id, updateDto);
                if (!updateResp.Successful || updateResp.Data == null) {
                    return PublishResult.Fail($"更新文章失败: {updateResp?.Message ?? "未知错误"}");
                }

                // 重新获取文章详情
                var detailResp = await _api.BlogPost.Get(blogPost.Id);
                if (!string.IsNullOrWhiteSpace(detailResp.Data?.Content)) {
                    blogPost = detailResp.Data;
                }
            }

            onProgress?.Invoke(100, "发布完成");
            return PublishResult.Ok(blogPost);
        }
        catch (Exception ex) {
            return PublishResult.Fail($"发布失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 分析文章中的本地图片
    /// </summary>
    /// <param name="filePath">Markdown 文件路径</param>
    /// <param name="content">文章内容</param>
    /// <returns>找到的本地图片路径列表</returns>
    public string[] AnalyzeImages(string filePath, string content) {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(content)) {
            return Array.Empty<string>();
        }

        var blogPost = new BlogPost { Content = content };
        var processor = new MarkdownProcessor(filePath, blogPost);
        return processor.ExtractImagePaths();
    }

    /// <summary>
    /// 获取文章详情
    /// </summary>
    public async Task<PublishResult> GetPostAsync(string id) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return PublishResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.BlogPost.Get(id);
            if (resp.Data == null) return PublishResult.Fail(resp.Message ?? "文章不存在");
            return PublishResult.Ok(resp.Data);
        }
        catch (Exception ex) {
            return PublishResult.Fail($"获取文章失败: {ex.Message}");
        }
    }
}

public class PublishResult {
    public bool Success { get; init; }
    public BlogPost? Post { get; init; }
    public string? ErrorMessage { get; init; }
    public string? PostUrl { get; init; }

    public static PublishResult Ok(BlogPost post) => new() { Success = true, Post = post };
    public static PublishResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
