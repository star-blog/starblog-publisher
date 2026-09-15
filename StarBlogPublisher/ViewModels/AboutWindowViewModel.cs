using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class AboutWindowViewModel : ViewModelBase {
    // 软件基本信息
    [ObservableProperty] private string _appName = "StarBlog Publisher";
    [ObservableProperty] private string _appVersion = $"版本 {ApplicationVersion.Value}";
    [ObservableProperty] private string _copyright = "© 2025–2026 DealiAxy · Licensed under Apache-2.0";
    [ObservableProperty] private string _description = "StarBlog Publisher 是面向 StarBlog 的跨平台文章发布工具，提供 Markdown 编辑、预览与发布、图片处理、AI 创作辅助和微信公众号排版。项目同时提供 CLI 与 MCP Server，便于自动化和 AI Agent 集成。";

    // 技术栈信息
    [ObservableProperty] private ObservableCollection<TechStackItem> _techStack = [
        new() { Name = ".NET 10", Description = "提供跨平台应用运行时" },
        new() { Name = "Avalonia UI 11.3", Description = "构建跨平台桌面界面" },
        new() { Name = "CommunityToolkit.Mvvm", Description = "提供 MVVM 模式与命令绑定" },
        new() { Name = "Markdig + Markdown.Avalonia", Description = "提供 Markdown 编辑与预览" },
        new() { Name = "Microsoft.Extensions.AI", Description = "集成多种 AI 服务" }
    ];

    // 相关链接
    [ObservableProperty] private ObservableCollection<LinkItem> _links = [
        new() { IconClass = "fa-solid fa-globe", IconColor = "#2196F3", Text = "访问项目主页", Url = "https://github.com/star-blog/starblog-publisher" },
        new() { IconClass = "fa-solid fa-book", IconColor = "#4CAF50", Text = "查看使用文档", Url = "https://github.com/star-blog/starblog-publisher#readme" },
        new() { IconClass = "fa-solid fa-download", IconColor = "#9C27B0", Text = "下载发布版本", Url = "https://github.com/star-blog/starblog-publisher/releases" },
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
