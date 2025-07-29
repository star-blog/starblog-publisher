using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

/// <summary>
/// 图片画廊窗口
/// </summary>
public partial class ImageGalleryWindow : Window
{
    /// <summary>
    /// 初始化图片画廊窗口
    /// </summary>
    public ImageGalleryWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    /// <summary>
    /// 初始化图片画廊窗口并设置数据上下文
    /// </summary>
    /// <param name="viewModel">视图模型</param>
    public ImageGalleryWindow(ImageGalleryWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}