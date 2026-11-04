using System.Collections.Generic;

namespace StarBlogPublisher.Models;

/// <summary>StarBlog v1 collection response.</summary>
public class PageResult<T> {
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public PaginationMetadata ToPagination() => new() {
        PageNumber = Page,
        PageSize = PageSize,
        PageCount = TotalPages,
        TotalItemCount = TotalCount,
        HasPreviousPage = Page > 1,
        HasNextPage = Page < TotalPages
    };
}
