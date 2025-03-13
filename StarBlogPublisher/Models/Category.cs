using System.Collections.Generic;

namespace StarBlogPublisher.Models;

public class Category
{
    public string Name { get; set; } = string.Empty;
    public List<Category> Children { get; set; } = new();
}