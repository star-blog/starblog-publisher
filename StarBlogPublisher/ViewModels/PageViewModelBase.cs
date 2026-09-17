using CommunityToolkit.Mvvm.ComponentModel;
using SukiUI.Controls;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 侧栏页面 ViewModel 基类。Title / Icon 用于 SukiSideMenuItem。
/// </summary>
public abstract partial class PageViewModelBase : ViewModelBase, ISukiStackPageTitleProvider {
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _icon;

    protected PageViewModelBase(string title, string icon) {
        _title = title;
        _icon = icon;
    }
}
