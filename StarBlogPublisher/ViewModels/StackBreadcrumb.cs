namespace StarBlogPublisher.ViewModels;

/// <summary>Breadcrumb item for any root page with a secondary content stack.</summary>
public sealed class StackBreadcrumb {
    public required string Title { get; init; }
    public object? Target { get; init; }
}
