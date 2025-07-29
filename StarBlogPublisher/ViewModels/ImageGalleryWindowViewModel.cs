using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 图片画廊窗口视图模型
/// </summary>
public partial class ImageGalleryWindowViewModel : ViewModelBase
{
    /// <summary>
    /// 图片集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ImageInfo> _images = new();

    /// <summary>
    /// 图片数量
    /// </summary>
    [ObservableProperty]
    private int _imageCount = 0;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "准备就绪";

    /// <summary>
    /// 关联的窗口
    /// </summary>
    private Window? _window;

    /// <summary>
    /// 初始化图片画廊窗口视图模型
    /// </summary>
    public ImageGalleryWindowViewModel()
    {
    }

    /// <summary>
    /// 设置关联的窗口
    /// </summary>
    /// <param name="window">窗口实例</param>
    public void SetWindow(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// 加载图片列表
    /// </summary>
    /// <param name="imagePaths">图片路径列表</param>
    public void LoadImages(string[] imagePaths)
    {
        Images.Clear();
        
        foreach (var imagePath in imagePaths)
        {
            var imageInfo = ImageInfo.Create(imagePath);
            Images.Add(imageInfo);
        }
        
        ImageCount = Images.Count;
        StatusMessage = $"已加载 {ImageCount} 张图片";
    }

    /// <summary>
    /// 关闭窗口命令
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _window?.Close();
    }

    /// <summary>
    /// 刷新图片列表命令
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        StatusMessage = "正在刷新图片列表...";
        
        // 重新检查所有图片的状态
        for (int i = 0; i < Images.Count; i++)
        {
            var currentImage = Images[i];
            var updatedImage = ImageInfo.Create(currentImage.FilePath);
            Images[i] = updatedImage;
        }
        
        StatusMessage = $"刷新完成，共 {ImageCount} 张图片";
    }

    /// <summary>
    /// 打开文件夹命令
    /// </summary>
    /// <param name="filePath">文件路径</param>
    [RelayCommand]
    private void OpenFolder(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                // 在Windows资源管理器中选中文件
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
                StatusMessage = "已在资源管理器中打开文件位置";
            }
            else
            {
                StatusMessage = "文件不存在，无法打开";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开文件夹失败: {ex.Message}";
            // 输出完整的异常堆栈信息到控制台，便于调试
            Console.WriteLine($"打开文件夹异常详情:\n{ex}");
        }
    }

    /// <summary>
    /// 复制路径命令
    /// </summary>
    /// <param name="filePath">文件路径</param>
    [RelayCommand]
    private async Task CopyPath(string filePath)
    {
        try
        {
            if (_window != null)
            {
                var topLevel = TopLevel.GetTopLevel(_window);
                if (topLevel != null)
                {
                    await topLevel.Clipboard!.SetTextAsync(filePath);
                    StatusMessage = "文件路径已复制到剪贴板";
                }
                else
                {
                    StatusMessage = "无法访问剪贴板";
                }
            }
            else
            {
                StatusMessage = "窗口未初始化，无法访问剪贴板";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"复制路径失败: {ex.Message}";
            // 输出完整的异常堆栈信息到控制台，便于调试
            Console.WriteLine($"复制路径异常详情:\n{ex}");
        }
    }
}