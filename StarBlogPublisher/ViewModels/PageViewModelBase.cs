using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.ViewModels;

/// <summary>Sidebar page view-model base. Navigation uses Fluent symbols directly.</summary>
public abstract partial class PageViewModelBase : ViewModelBase {
    [ObservableProperty] private string _title;
    [ObservableProperty] private Symbol _navSymbol;

    protected PageViewModelBase(string title, Symbol navSymbol = Symbol.Document) {
        _title = title;
        _navSymbol = navSymbol;
    }
}
