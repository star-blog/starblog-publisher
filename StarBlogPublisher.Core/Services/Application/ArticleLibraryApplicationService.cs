using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 站点文章库：列表、更新、删除、拉取到本地。与首次发布流程分开。
/// </summary>
public class ArticleLibraryApplicationService {
    private readonly ApiService _api;
    private readonly AuthApplicationService _authService;

    public ArticleLibraryApplicationService(ApiService api, AuthApplicationService authService) {
        _api = api;
        _authService = authService;
    }

    public async Task<PostListResult> ListAsync(PostListQuery query) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return PostListResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.BlogPost.GetList(query);
            if (resp.Data == null) return PostListResult.Fail(resp.Message ?? "文章列表为空");
            return PostListResult.Ok(resp.Data, resp.Pagination ?? new PaginationMetadata {
                PageNumber = query.Page,
                PageSize = query.PageSize,
                TotalItemCount = resp.Data.Count
            });
        }
        catch (Exception ex) {
            return PostListResult.Fail($"获取文章列表失败: {ex.Message}");
        }
    }

    public async Task<PublishResult> GetAsync(string id) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return PublishResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.BlogPost.Get(id);
            if (resp.Data == null) return PublishResult.Fail(resp.Message ?? "文章不存在");
            return PublishResult.Ok(resp.Data, BuildPostUrl(resp.Data), resp.Data.Content);
        }
        catch (Exception ex) {
            return PublishResult.Fail($"获取文章失败: {ex.Message}");
        }
    }

    public async Task<PublishResult> UpdateAsync(string id, PostUpdateDto dto) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return PublishResult.Fail(authCheck.ErrorMessage!);
        if (string.IsNullOrWhiteSpace(id)) return PublishResult.Fail("文章 ID 不能为空");
        if (string.IsNullOrWhiteSpace(dto.Title)) return PublishResult.Fail("标题不能为空");
        if (string.IsNullOrWhiteSpace(dto.Content)) return PublishResult.Fail("文章内容为空");
        if (dto.CategoryId <= 0) return PublishResult.Fail("请选择文章分类");

        try {
            dto.Id = id;
            var resp = await _api.BlogPost.Update(id, dto);
            if (resp.Data == null) return PublishResult.Fail(resp.Message ?? "更新文章失败");
            return PublishResult.Ok(resp.Data, BuildPostUrl(resp.Data), resp.Data.Content);
        }
        catch (Exception ex) {
            return PublishResult.Fail($"更新文章失败: {ex.Message}");
        }
    }

    public async Task<LibraryActionResult> DeleteAsync(string id) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return LibraryActionResult.Fail(authCheck.ErrorMessage!);
        if (string.IsNullOrWhiteSpace(id)) return LibraryActionResult.Fail("文章 ID 不能为空");

        try {
            var resp = await _api.BlogPost.Delete(id);
            if (!resp.IsOk) return LibraryActionResult.Fail(resp.Message ?? "删除文章失败");
            return LibraryActionResult.Ok(resp.Message ?? "已删除");
        }
        catch (Exception ex) {
            return LibraryActionResult.Fail($"删除文章失败: {ex.Message}");
        }
    }

    public async Task<LibraryActionResult> PullAsync(string id, string markdownPath) {
        var result = await GetAsync(id);
        if (!result.Success || result.Post == null) {
            return LibraryActionResult.Fail(result.ErrorMessage ?? "拉取失败");
        }

        try {
            var directory = Path.GetDirectoryName(markdownPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var post = result.Post;
            await File.WriteAllTextAsync(markdownPath, post.Content ?? string.Empty);
            await ArticleSidecar.WriteAsync(markdownPath, new ArticleSidecar {
                Title = post.Title,
                Description = post.Summary ?? string.Empty,
                Slug = post.Slug ?? string.Empty,
                Category = post.Category ?? new Category { Id = post.CategoryId },
                PostId = post.Id,
                IsPublish = post.IsPublish,
                LastSyncedAt = DateTime.UtcNow
            });
            return LibraryActionResult.Ok(markdownPath, post);
        }
        catch (Exception ex) {
            return LibraryActionResult.Fail($"保存本地文件失败: {ex.Message}");
        }
    }

    public string BuildPostUrl(BlogPost post) {
        var baseUrl = _api.BaseUrl.TrimEnd('/');
        return !string.IsNullOrWhiteSpace(post.Slug)
            ? $"{baseUrl}/p/{post.Slug}"
            : $"{baseUrl}/Blog/Post/{post.Id}";
    }
}

public class PostListResult {
    public bool Success { get; init; }
    public List<BlogPost> Posts { get; init; } = [];
    public PaginationMetadata Pagination { get; init; } = new();
    public string? ErrorMessage { get; init; }

    public static PostListResult Ok(List<BlogPost> posts, PaginationMetadata pagination) => new() {
        Success = true,
        Posts = posts,
        Pagination = pagination
    };

    public static PostListResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public class LibraryActionResult {
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
    public BlogPost? Post { get; init; }

    public static LibraryActionResult Ok(string message, BlogPost? post = null) => new() {
        Success = true,
        Message = message,
        Post = post
    };

    public static LibraryActionResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
