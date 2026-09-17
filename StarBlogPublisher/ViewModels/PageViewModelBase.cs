using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 侧栏页面 ViewModel 基类。Title / NavSymbol 用于 NavigationViewItem。
/// </summary>
public abstract partial class PageViewModelBase : ViewModelBase {
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private Symbol _navSymbol;

    protected PageViewModelBase(string title, string icon, Symbol navSymbol = Symbol.Document) {
        _title = title;
        _icon = icon;
        _navSymbol = navSymbol;
    }
}
