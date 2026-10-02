namespace StarBlogPublisher.Models.Dtos;

/// <summary>Query string for <c>GET /Api/BlogPost</c>.</summary>
public class PostListQuery {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public int CategoryId { get; set; }
    public bool? IsPublish { get; set; }
    public string? SortBy { get; set; } = "-LastUpdateTime";
}
