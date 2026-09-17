using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class ImageGalleryViewModel : ViewModelBase {
    public string Title => "图片分析";

    [ObservableProperty] private ObservableCollection<AvaloniaImageInfo> _images = new();
    [ObservableProperty] private int _imageCount;
    [ObservableProperty] private string _statusMessage = "准备就绪";

    public void LoadImages(string[] imagePaths) {
        Images.Clear();
        foreach (var imagePath in imagePaths) {
            Images.Add(AvaloniaImageInfo.Create(imagePath));
        }

        ImageCount = Images.Count;
        StatusMessage = $"已加载 {ImageCount} 张图片";
    }

    [RelayCommand]
    private void Refresh() {
        StatusMessage = "正在刷新图片列表...";
        for (var i = 0; i < Images.Count; i++) {
            Images[i] = AvaloniaImageInfo.Create(Images[i].FilePath);
        }

        StatusMessage = $"刷新完成，共 {ImageCount} 张图片";
    }

    [RelayCommand]
    private void OpenFolder(string filePath) {
        try {
            if (File.Exists(filePath)) {
                Process.Start(new ProcessStartInfo {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
                StatusMessage = "已在资源管理器中打开文件位置";
            }
            else {
                StatusMessage = "文件不存在，无法打开";
            }
        }
        catch (Exception ex) {
            StatusMessage = $"打开文件夹失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyPath(string filePath) {
        try {
            var clipboard = GuiHost.GetTopLevel()?.Clipboard;
            if (clipboard == null) {
                StatusMessage = "无法访问剪贴板";
                return;
            }

            await clipboard.SetTextAsync(filePath);
            StatusMessage = "文件路径已复制到剪贴板";
            GuiHost.ToastSuccess("已复制", "文件路径已复制到剪贴板");
        }
        catch (Exception ex) {
            StatusMessage = $"复制路径失败: {ex.Message}";
        }
    }
}
