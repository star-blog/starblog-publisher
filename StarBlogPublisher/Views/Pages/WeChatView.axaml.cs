using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class WeChatView : UserControl {
    public WeChatView() {
        InitializeComponent();
    }

    private void OnBreadcrumbClicked(object? sender, int index) {
        if (DataContext is WeChatViewModel vm) {
            vm.NavigateBreadcrumbAt(index);
        }
    }
}
