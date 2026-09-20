using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

/// <summary>侧栏主导航与底部操作项共用的展示契约。</summary>
public interface INavigationMenuItem {
    string Title { get; }
    Icon NavIcon { get; }
    string ToolTip { get; }
    bool SelectsOnInvoked { get; }
    string? Tag { get; }
}
