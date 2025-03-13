using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace StarBlogPublisher.ViewModels;

public partial class AboutWindowViewModel : ViewModelBase
{
    // 软件基本信息
    [ObservableProperty] private string _appName = "StarBlog Publisher";
    [ObservableProperty] private string _appVersion = "版本 1.0.0";
    [ObservableProperty] private string _copyright = "© 2024 StarBlog Publisher. All rights reserved.";
    [ObservableProperty] private string _description = "StarBlog Publisher 是一款专业的博客文章发布工具，支持Markdown格式文章的预览和发布。";
    
    // 技术栈信息
    [ObservableProperty] private ObservableCollection<TechStackItem> _techStack;
    
    // 相关链接
    [ObservableProperty] private ObservableCollection<LinkItem> _links;
    
    public AboutWindowViewModel()
    {
        // 初始化技术栈信息
        _techStack = new ObservableCollection<TechStackItem>
        {
            new TechStackItem { Name = ".NET 8", Description = "开发" },
            new TechStackItem { Name = "Avalonia UI", Description = "开发" },
            new TechStackItem { Name = "Markdown.Avalonia", Description = "提供Markdown渲染支持" }
        };
        
        // 初始化相关链接
        _links = new ObservableCollection<LinkItem>
        {
            new LinkItem { Icon = "🌐", Text = "访问项目主页", Url = "https://github.com/yourusername/starblog-publisher" },
            new LinkItem { Icon = "📖", Text = "查看文档", Url = "https://github.com/yourusername/starblog-publisher/wiki" },
            new LinkItem { Icon = "🐛", Text = "报告问题", Url = "https://github.com/yourusername/starblog-publisher/issues" }
        };
    }
}

// 技术栈项目类
public class TechStackItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// 链接项目类
public class LinkItem
{
    public string Icon { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}