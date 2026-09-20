using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

/// <summary>Sidebar page view-model base. Navigation uses Fluent symbols directly.</summary>
public abstract partial class PageViewModelBase : ViewModelBase, INavigationMenuItem {
    [ObservableProperty] private string _title;
    [ObservableProperty] private Icon _navIcon;

    public virtual string ToolTip => Title;
    public virtual bool SelectsOnInvoked => true;
    public virtual string? Tag => null;

    protected PageViewModelBase(string title, Icon navIcon = Icon.Document) {
        _title = title;
        _navIcon = navIcon;
    }
}
