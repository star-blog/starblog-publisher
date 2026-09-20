using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

/// <summary>侧栏底部动作项（主题切换、登录/登出），不是页面导航。</summary>
public partial class ShellFooterNavItem : ObservableObject, INavigationMenuItem {
    public required string Tag { get; init; }

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private Icon _navIcon;
    [ObservableProperty] private string _toolTip = "";

    public bool SelectsOnInvoked => false;
}
