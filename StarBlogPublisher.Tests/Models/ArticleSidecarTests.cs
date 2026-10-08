using FluentAssertions;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Tests.Models;

public class ArticleSidecarTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "starblog-sidecar-tests", Guid.NewGuid().ToString("N"));

    public ArticleSidecarTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task WriteAsync_UsesIndentedUtf8Chinese_AndOmitsNullIdentity() {
        var path = Path.Combine(_directory, "旦家园.md");
        await File.WriteAllTextAsync(path, "# 旦家园");

        await ArticleSidecar.WriteAsync(path, new ArticleSidecar {
            Title = "旦家园",
            Description = "前言",
            Slug = "dan-jia-yuan",
            Keywords = "汕头"
        });

        var json = await File.ReadAllTextAsync(ArticleSidecar.PathFor(path));
        json.Should().Contain("\n");
        json.Should().Contain("\"Title\": \"旦家园\"");
        json.Should().Contain("\"Description\": \"前言\"");
        json.Should().NotContain("\\u");
        json.Should().NotContain("PostId");
        json.Should().NotContain("Category");
        json.Should().NotContain("IsPublish");
        json.Should().NotContain("LastSyncedAt");
    }

    [Fact]
    public async Task WriteAsync_StripsCategoryTreeAndRoundTripsIdentity() {
        var path = Path.Combine(_directory, "pulled.md");
        var synced = new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc);
        await ArticleSidecar.WriteAsync(path, new ArticleSidecar {
            Title = "已同步",
            Category = new Category {
                Id = 17,
                Text = "技术",
                Nodes = [new Category { Id = 18, Text = "子分类" }]
            },
            PostId = "p-42",
            IsPublish = true,
            LastSyncedAt = synced
        });

        var json = await File.ReadAllTextAsync(ArticleSidecar.PathFor(path));
        json.Should().Contain("\"PostId\": \"p-42\"");
        json.Should().Contain("\"Text\": \"技术\"");
        json.Should().NotContain("子分类");
        json.Should().NotContain("Nodes");

        var sidecar = await ArticleSidecar.ReadAsync(path);
        sidecar.Should().NotBeNull();
        sidecar!.Title.Should().Be("已同步");
        sidecar.PostId.Should().Be("p-42");
        sidecar.IsPublish.Should().BeTrue();
        sidecar.LastSyncedAt.Should().Be(synced);
        sidecar.Category!.Id.Should().Be(17);
        sidecar.Category.Text.Should().Be("技术");
        sidecar.Category.Nodes.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_AcceptsLegacyCompactUnicodeEscapes() {
        var path = Path.Combine(_directory, "legacy.md");
        await File.WriteAllTextAsync(path, "# body");
        await File.WriteAllTextAsync(ArticleSidecar.PathFor(path),
            """{"Title":"\u65E6\u5BB6\u56ED","Description":"摘要","Slug":"","Keywords":"","Category":null}""");

        var sidecar = await ArticleSidecar.ReadAsync(path);
        sidecar.Should().NotBeNull();
        sidecar!.Title.Should().Be("旦家园");
        sidecar.Description.Should().Be("摘要");
        sidecar.Category.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenMissing() {
        var path = Path.Combine(_directory, "missing.md");
        (await ArticleSidecar.ReadAsync(path)).Should().BeNull();
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
