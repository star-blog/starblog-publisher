using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StarBlogPublisher.Models;

/// <summary>Local companion file written next to a pulled Markdown article.</summary>
public class ArticleSidecar {
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public Category? Category { get; set; }
    public string? PostId { get; set; }
    public bool? IsPublish { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public static string PathFor(string markdownPath) => markdownPath + ".starblog.json";

    public static async Task WriteAsync(string markdownPath, ArticleSidecar sidecar) {
        var json = JsonSerializer.Serialize(sidecar, SidecarJsonOptions);
        await File.WriteAllTextAsync(PathFor(markdownPath), json, new UTF8Encoding(false));
    }

    public static async Task<ArticleSidecar?> ReadAsync(string markdownPath) {
        var path = PathFor(markdownPath);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ArticleSidecar>(await File.ReadAllTextAsync(path), SidecarJsonOptions);
    }

    private static readonly JsonSerializerOptions SidecarJsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
