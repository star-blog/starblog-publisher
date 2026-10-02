using System.Collections.Generic;

namespace StarBlogPublisher.Models;

public class Category
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public string? Name { get; set; }
    public string? Href { get; set; }
    public int ParentId { get; set; }
    public bool Visible { get; set; } = true;
    public List<string> Tags { get; set; } = [];
    public List<Category>? Nodes { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(Text) ? Text : Name ?? $"#{Id}";
}
