using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views.Controls;

namespace StarBlogPublisher.Views;

public partial class PublishView : Avalonia.Controls.UserControl {
    public PublishView() {
        InitializeComponent();
    }

    private void OnBreadcrumbClicked(object? sender, int index) {
        if (DataContext is PublishViewModel vm) {
            vm.NavigateBreadcrumbAt(index);
        }
    }
}
