using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace StarBlogPublisher.ViewModels;

public partial class AboutWindowViewModel : ViewModelBase {
    // 软件基本信息
    [ObservableProperty] private string _appName = "StarBlog Publisher";
    [ObservableProperty] private string _appVersion = "版本 1.10.2";
    [ObservableProperty] private string _copyright = "© 2025 DealiAxy. All rights reserved.";
    [ObservableProperty] private string _description = "StarBlog Publisher 是一款专业的博客文章发布工具，支持Markdown格式文章的预览和发布。";

    // 技术栈信息
    [ObservableProperty] private ObservableCollection<TechStackItem> _techStack = [
        new() { Name = ".NET 8", Description = "开发" },
        new() { Name = "Avalonia UI", Description = "开发" },
        new() { Name = "Markdown.Avalonia", Description = "提供Markdown渲染支持" }
    ];

    // 相关链接
    [ObservableProperty] private ObservableCollection<LinkItem> _links = [
        new() { IconClass = "fa-solid fa-globe", IconColor = "#2196F3", Text = "访问项目主页", Url = "https://github.com/star-blog/starblog-publisher" },
        new() { IconClass = "fa-solid fa-book", IconColor = "#4CAF50", Text = "查看文档", Url = "https://github.com/star-blog/starblog-publisher/wiki" },
        new() { IconClass = "fa-solid fa-bug", IconColor = "#F44336", Text = "报告问题", Url = "https://github.com/star-blog/starblog-publisher/issues" }
    ];
}

// 技术栈项目类
public class TechStackItem {
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// 链接项目类
public partial class LinkItem : ObservableObject {
    public string IconClass { get; set; } = string.Empty;
    public string IconColor { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    [RelayCommand]
    private void OpenLink(string url) {
        if (string.IsNullOrEmpty(url)) return;
        try {
            var psi = new ProcessStartInfo {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch {
            // 处理打开链接失败的情况
            Debug.WriteLine($"Failed to open link: {url}");
        }
    }
}