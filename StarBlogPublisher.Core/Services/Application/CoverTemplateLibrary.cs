using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

public sealed record CoverPreset(string Id, string Name, bool IsColumn, bool IncludeBackground, CoverDocument Design);
public sealed record CoverLibraryIndex {
    public int Version { get; init; } = 1;
    public List<CoverPreset> Presets { get; init; } = [];
    public List<string> Recent { get; init; } = [];
}
[JsonSerializable(typeof(CoverLibraryIndex))]
public partial class CoverLibraryJson : JsonSerializerContext;

/// <summary>Durable, local template library. Article documents never depend on its asset lifetime.</summary>
public sealed class CoverTemplateLibrary(string directory) {
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string IndexPath => Path.Combine(DirectoryPath, "library.json");
    private SemaphoreSlim Gate => Gates.GetOrAdd(DirectoryPath, _ => new(1));
    public async Task<CoverLibraryIndex> ReadAsync(CancellationToken token = default) {
        await Gate.WaitAsync(token);
        try { return await ReadCoreAsync(token); } finally { Gate.Release(); }
    }
    private async Task<CoverLibraryIndex> ReadCoreAsync(CancellationToken token) {
        if (!File.Exists(IndexPath)) return new();
        var index = JsonSerializer.Deserialize(await File.ReadAllTextAsync(IndexPath, token), CoverLibraryJson.Default.CoverLibraryIndex)
            ?? throw new InvalidOperationException("模板库无法读取，原文件已保留");
        if (index.Version != 1) throw new InvalidOperationException("此模板库版本暂不可编辑，原文件已保留");
        if (index.Presets.Any(p => !p.Design.IsSupported)) throw new InvalidOperationException("模板库包含暂不支持的设计版本，原文件已保留");
        return index;
    }
    private async Task WriteAsync(CoverLibraryIndex index, CancellationToken token) {
        Directory.CreateDirectory(DirectoryPath);
        var temp = IndexPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(index, CoverLibraryJson.Default.CoverLibraryIndex), token);
            token.ThrowIfCancellationRequested(); File.Move(temp, IndexPath, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public async Task<CoverPreset> SaveAsync(string name, bool column, bool includeBackground, CoverDocument source, string? articlePath,
        string? replaceId = null, CancellationToken token = default) {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("请填写模板名称");
        if (!source.IsSupported || source.Elements == null) throw new InvalidOperationException("请先升级为独立对象再保存模板");
        await Gate.WaitAsync(token);
        try {
            var index = await ReadCoreAsync(token);
            var assets = new CoverAssetService(Path.Combine(DirectoryPath, "assets"));
            async Task<string?> Copy(string? path) => path == null ? null : Path.GetRelativePath(DirectoryPath,
                await assets.ImportAsync(await File.ReadAllBytesAsync(CoverAssetService.Resolve(path, articlePath), token), token)).Replace('\\', '/');
            var design = source with { Title = "", Subtitle = "", Prompt = "", Logo = await Copy(source.Logo),
                Background = includeBackground ? await Copy(source.Background) : null };
            var preset = new CoverPreset(replaceId ?? Guid.NewGuid().ToString("N"), name.Trim(), column, includeBackground, design);
            var values = index.Presets.Where(p => p.Id != preset.Id).ToList(); values.Add(preset);
            await WriteAsync(index with { Presets = values }, token); return preset;
        } finally { Gate.Release(); }
    }
    public async Task<CoverDocument> InstantiateAsync(CoverPreset preset, CoverDocument destination, string articleTitle,
        CoverAssetService assets, CancellationToken token = default) {
        if (!preset.Design.IsSupported) throw new InvalidOperationException("此模板版本暂不可使用");
        async Task<string?> Copy(string? path) => path == null ? null : await assets.ImportAsync(
            await File.ReadAllBytesAsync(CoverAssetService.Resolve(path, IndexPath), token), token);
        var background = preset.IncludeBackground ? await Copy(preset.Design.Background) : destination.Background;
        var logo = await Copy(preset.Design.Logo);
        token.ThrowIfCancellationRequested();
        return preset.Design with { Title = articleTitle, Subtitle = "", Background = background, Logo = logo,
            BackgroundName = preset.IncludeBackground ? preset.Design.BackgroundName : destination.BackgroundName,
            BackgroundX = preset.IncludeBackground ? preset.Design.BackgroundX : 0,
            BackgroundY = preset.IncludeBackground ? preset.Design.BackgroundY : 0, Zoom = preset.IncludeBackground ? preset.Design.Zoom : 1 };
    }
    public async Task RenameAsync(string id, string name, CancellationToken token = default) {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("请填写模板名称");
        await MutateAsync(i => i with { Presets = i.Presets.Select(p => p.Id == id ? p with { Name = name.Trim() } : p).ToList() }, token);
    }
    public Task DeleteAsync(string id, CancellationToken token = default) => MutateAsync(i => i with {
        Presets = i.Presets.Where(p => p.Id != id).ToList(), Recent = i.Recent.Where(r => r != id).ToList()
    }, token);
    public Task MarkUsedAsync(string id, CancellationToken token = default) => MutateAsync(i => i with {
        Recent = new[] { id }.Concat(i.Recent.Where(r => r != id)).Take(8).ToList()
    }, token);
    private async Task MutateAsync(Func<CoverLibraryIndex, CoverLibraryIndex> update, CancellationToken token) {
        await Gate.WaitAsync(token);
        try { await WriteAsync(update(await ReadCoreAsync(token)), token); } finally { Gate.Release(); }
    }
}
