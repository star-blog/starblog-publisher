namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 发布页（及其他二级栈）面包屑项。Target 为 null 表示回到根页。
/// </summary>
public sealed class PublishBreadcrumb {
    public required string Title { get; init; }
    public object? Target { get; init; }
}
