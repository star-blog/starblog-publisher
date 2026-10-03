using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class SitePostsView : UserControl {
    public SitePostsView() {
        InitializeComponent();
    }

    private async void OnBreadcrumbClicked(object? sender, int index) {
        if (DataContext is SitePostsViewModel vm) await vm.NavigateBreadcrumbAsync(index);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Enter && DataContext is SitePostsViewModel vm) {
            vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPostDoubleTapped(object? sender, TappedEventArgs e) {
        if (DataContext is SitePostsViewModel vm) vm.OpenSelectedCommand.Execute(null);
    }
}
