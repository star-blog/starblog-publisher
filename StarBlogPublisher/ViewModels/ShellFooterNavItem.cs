using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 侧栏底部操作项（主题、登录）。须为数据对象，由 NavigationView MenuItemTemplate 生成 FANavigationViewItem。
/// </summary>
public partial class ShellFooterNavItem : ObservableObject, INavigationMenuItem {
    public required string Tag { get; init; }

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private Icon _navIcon;
    [ObservableProperty] private string _toolTip = "";

    public bool SelectsOnInvoked => false;
}
