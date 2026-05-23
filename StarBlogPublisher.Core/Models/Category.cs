using System.Collections.Generic;

namespace StarBlogPublisher.Models;

public class Category
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public string? Href { get; set; }
    public List<string> Tags { get; set; }
    public List<Category>? Nodes { get; set; }
}