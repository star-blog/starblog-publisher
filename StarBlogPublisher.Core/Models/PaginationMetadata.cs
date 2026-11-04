namespace StarBlogPublisher.Models;

/// <summary>Pagination information used by the article library UI and CLI.</summary>
public class PaginationMetadata {
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int PageCount { get; set; }
    public int TotalItemCount { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    public int TotalPages => PageCount > 0
        ? PageCount
        : PageSize <= 0 ? 0 : (int)System.Math.Ceiling(TotalItemCount / (double)PageSize);
}
