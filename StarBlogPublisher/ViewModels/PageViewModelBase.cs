using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace StarBlogPublisher.ViewModels;

/// <summary>Sidebar page view-model base. Navigation uses Fluent symbols directly.</summary>
public abstract partial class PageViewModelBase : ViewModelBase {
    [ObservableProperty] private string _title;
    [ObservableProperty] private Icon _navIcon;

    protected PageViewModelBase(string title, Icon navIcon = Icon.Document) {
        _title = title;
        _navIcon = navIcon;
    }
}
