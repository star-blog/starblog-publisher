using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class SettingsViewModel {
    [RelayCommand]
    private async Task ExportSettings() {
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;

        var suggestedName = $"starblog-settings-{DateTime.Now:yyyyMMdd}.json";
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "导出配置",
            SuggestedFileName = suggestedName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("StarBlog 配置") { Patterns = ["*.json"] }]
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        try {
            var json = AppSettingsPortableTransfer.ExportJson(AppSettings.Instance);
            await WriteFileAtomicallyAsync(path, json);
            GuiHost.ToastSuccess("备份", "配置已导出");
        }
        catch (Exception ex) {
            GuiHost.ToastError("导出失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportSettings() {
        if (HasChanges) {
            if (!await GuiHost.ConfirmAsync(
                    "放弃未保存的更改？",
                    "导入将立即覆盖当前已保存的配置。你在设置页中尚未保存的编辑将被丢弃。")) {
                return;
            }
        }

        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "导入配置",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("StarBlog 配置") { Patterns = ["*.json"] }]
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrEmpty(path)) return;

        string json;
        try {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex) {
            GuiHost.ToastError("导入失败", ex.Message);
            return;
        }

        if (!AppSettingsPortableTransfer.TryParse(json, out var document, out var parseError)) {
            GuiHost.ToastError("导入失败", parseError ?? "无法解析配置文件。");
            return;
        }

        var dialog = new FAContentDialog {
            Title = "导入配置？",
            Content = "将覆盖当前已保存的全部设置（含账号密码与 API 密钥），并立即生效。登录会话与文章工作区不受影响。",
            PrimaryButtonText = "导入并应用",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Close
        };
        if (await dialog.ShowAsync(GuiHost.GetMainWindow()) != FAContentDialogResult.Primary) {
            return;
        }

        if (!AppSettingsPortableTransfer.TryApply(AppSettings.Instance, document!, out var applyError)) {
            GuiHost.ToastError("导入失败", applyError ?? "无法应用配置。");
            return;
        }

        Reload();
        _shell.ApplyTheme(ThemeMode);
        _shell.PublishPage.NotifyAiEnabled();
        foreach (var documentVm in _shell.Workspace.Documents) documentVm.NotifyAiEnabled();
        GuiHost.ToastSuccess("备份", "配置已导入并应用");
    }

    private static async Task WriteFileAtomicallyAsync(string path, string content) {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await File.WriteAllTextAsync(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally {
            if (File.Exists(temporaryPath)) {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
