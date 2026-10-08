using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Models;

public partial class ArticleCoverImage(ArticleImageSource image) : ObservableObject, IDisposable {
    public string Source => image.Source;
    public string Name => image.Name;
    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private string _status = "正在加载…";

    public void Dispose() {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}
