using System;
using System.IO;
using System.Text.Json;

namespace StarBlogPublisher.Services;

public sealed record WorkspaceHistory(string[] RecentFiles, string[] OpenFiles, string? ActiveFile);

public sealed class WorkspaceHistoryStore(string path) {
    public WorkspaceHistory Load() {
        try {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<WorkspaceHistory>(File.ReadAllText(path)) ?? new([], [], null)
                : new([], [], null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            return new([], [], null);
        }
    }

    public void Save(WorkspaceHistory history) {
        var temporaryPath = path + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(history));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            GuiHost.ToastWarning("工作区记录未保存", ex.Message);
        }
    }
}
