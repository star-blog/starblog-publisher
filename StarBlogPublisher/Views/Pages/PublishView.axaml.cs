using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class PublishView : UserControl {
    public PublishView() {
        InitializeComponent();
    }

    private void OnBreadcrumbItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args) {
        if (DataContext is PublishViewModel vm) {
            vm.NavigateBreadcrumbAt(args.Index);
        }
    }
}
