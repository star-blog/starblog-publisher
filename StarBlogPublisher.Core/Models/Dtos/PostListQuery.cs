using Refit;

namespace StarBlogPublisher.Models.Dtos;

/// <summary>Query string for <c>GET /api/v1/posts</c>.</summary>
public class PostListQuery {
    [AliasAs("page")] public int Page { get; set; } = 1;
    [AliasAs("pageSize")] public int PageSize { get; set; } = 20;
    [AliasAs("search")] public string? Search { get; set; }
    [AliasAs("categoryId")] public int CategoryId { get; set; }
    [AliasAs("isPublish")] public bool? IsPublish { get; set; }
    [AliasAs("sortBy")] public string? SortBy { get; set; } = "LastUpdateTime";
    [AliasAs("sortType")] public string? SortType { get; set; } = "desc";
    [AliasAs("includeDescendants")] public bool IncludeDescendants { get; set; }
}
