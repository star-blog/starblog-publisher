namespace StarBlogPublisher.Models;

/// <summary>The fields needed to download a random photo from the current StarBlog site.</summary>
public class SitePhoto {
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
