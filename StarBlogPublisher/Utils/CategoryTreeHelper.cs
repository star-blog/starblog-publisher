using System;
using System.Collections.Generic;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Utils;

internal static class CategoryTreeHelper {
    public static List<Category> Filter(IEnumerable<Category>? categories, string query) {
        var result = new List<Category>();
        if (categories == null) return result;
        foreach (var category in categories) {
            var children = Filter(category.Nodes, query);
            if (category.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)) {
                result.Add(category);
            }
            else if (children.Count > 0) {
                result.Add(new Category {
                    Id = category.Id, Text = category.Text, Name = category.Name,
                    ParentId = category.ParentId, Visible = category.Visible,
                    Href = category.Href, Tags = category.Tags, Nodes = children
                });
            }
        }
        return result;
    }

    public static Category? FindById(IEnumerable<Category>? categories, int id) {
        if (categories == null) return null;
        foreach (var category in categories) {
            if (category.Id == id) return category;
            if (FindById(category.Nodes, id) is { } child) return child;
        }
        return null;
    }
}
