using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

public enum ShellPageId {
    WeChat,
    Settings,
    About,
}

/// <summary>Lightweight sidebar entry before the corresponding page view-model is created.</summary>
public sealed class ShellPageNavItem : INavigationMenuItem {
    public ShellPageNavItem(ShellPageId pageId, string title, Icon navIcon) {
        PageId = pageId;
        Title = title;
        NavIcon = navIcon;
    }

    public ShellPageId PageId { get; }
    public string Title { get; }
    public Icon NavIcon { get; }
    public string ToolTip => Title;
    public bool SelectsOnInvoked => true;
    public string? Tag => PageId.ToString();
}
