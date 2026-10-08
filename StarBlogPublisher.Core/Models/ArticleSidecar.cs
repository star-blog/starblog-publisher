using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StarBlogPublisher.Models;

/// <summary>
/// Companion file next to a Markdown article: publish properties plus optional site identity.
/// </summary>
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
        var path = PathFor(markdownPath);
        var json = JsonSerializer.Serialize(CloneForWrite(sidecar), SidecarJsonOptions);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static async Task<ArticleSidecar?> ReadAsync(string markdownPath) {
        var path = PathFor(markdownPath);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ArticleSidecar>(await File.ReadAllTextAsync(path), SidecarJsonOptions);
    }

    internal static readonly JsonSerializerOptions SidecarJsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static ArticleSidecar CloneForWrite(ArticleSidecar sidecar) => new() {
        Title = sidecar.Title,
        Description = sidecar.Description,
        Slug = sidecar.Slug,
        Keywords = sidecar.Keywords,
        Category = SnapshotCategory(sidecar.Category),
        PostId = sidecar.PostId,
        IsPublish = sidecar.IsPublish,
        LastSyncedAt = sidecar.LastSyncedAt
    };

    internal static Category? SnapshotCategory(Category? category) {
        if (category is null) return null;
        var text = !string.IsNullOrWhiteSpace(category.Text) ? category.Text : category.Name;
        return new Category { Id = category.Id, Text = text };
    }
}
