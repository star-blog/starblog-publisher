using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

public sealed class CoverAssetService(string cacheDirectory) {
    public string CacheDirectory { get; } = cacheDirectory;
    public static string Resolve(string asset, string? articlePath) => Path.IsPathRooted(asset) ? asset :
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(articlePath ?? "") ?? Environment.CurrentDirectory, asset));
    public async Task<string> ImportAsync(byte[] bytes, CancellationToken token = default) {
        if (bytes.Length > 10 * 1024 * 1024) throw new InvalidOperationException("图片不能超过 10 MB");
        ImagePreview.EnsureDecodable(bytes);
        Directory.CreateDirectory(CacheDirectory);
        var path = Path.Combine(CacheDirectory, Convert.ToHexString(SHA256.HashData(bytes)) + ".image");
        await WriteOnceAsync(path, bytes, token);
        return path;
    }
    public async Task<ArticleCoverState?> PersistAsync(ArticleCoverState? state, string? sourcePath, string destination, CancellationToken token = default) {
        if (state == null || !state.IsSupported) return state;
        async Task<CoverDocument> Copy(CoverDocument d) {
            async Task<string?> Asset(string? source) {
                if (source == null) return null;
                var bytes = await File.ReadAllBytesAsync(Resolve(source, sourcePath), token);
                var directory = destination + ".starblog-assets" + Path.DirectorySeparatorChar + "cover";
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(bytes)) + ".image");
                await WriteOnceAsync(path, bytes, token);
                return Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(destination))!, path).Replace('\\', '/');
            }
            return d with { Background = await Asset(d.Background), Logo = await Asset(d.Logo) };
        }
        return state with { Draft = await Copy(state.Draft), Applied = state.Applied == null ? null : await Copy(state.Applied) };
    }
    private static async Task WriteOnceAsync(string path, byte[] bytes, CancellationToken token) {
        if (File.Exists(path)) return;
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, bytes, token); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
