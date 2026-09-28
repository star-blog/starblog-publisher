namespace StarBlogPublisher.Services.Application;

/// <summary>Optional draft/add fields collected on the WeChat formatting page.</summary>
public sealed class WeChatDraftMetadata {
    public string? Author { get; init; }
    public string? ContentSourceUrl { get; init; }
    public bool OpenComment { get; init; } = true;
    public bool FansOnlyComment { get; init; }
    public int CoverWidth { get; init; }
    public int CoverHeight { get; init; }
}
