using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

public sealed record CoverRecovery(string Id, string? ArticlePath, string? SourcePath, string Baseline, string Markdown, ArticleSidecar Properties);
[JsonSerializable(typeof(CoverRecovery))]
internal partial class CoverRecoveryJson : JsonSerializerContext;

public sealed class CoverRecoveryStore(string directory) {
    public string[] Entries => Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : [];
    public CoverRecovery? Read(string path) => JsonSerializer.Deserialize(File.ReadAllText(path), CoverRecoveryJson.Default.CoverRecovery);
    public static string Fingerprint(string? path) {
        if (path == null || !File.Exists(path)) return "";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(File.ReadAllBytes(path));
        if (File.Exists(ArticleSidecar.PathFor(path))) hash.AppendData(File.ReadAllBytes(ArticleSidecar.PathFor(path)));
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public async Task WriteAsync(CoverRecovery recovery, CancellationToken token) {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, recovery.Id + ".json"); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(recovery, CoverRecoveryJson.Default.CoverRecovery), token);
            token.ThrowIfCancellationRequested();
            File.Move(temp, path, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Delete(string id) { var path = Path.Combine(directory, id + ".json"); if (File.Exists(path)) File.Delete(path); }
}
