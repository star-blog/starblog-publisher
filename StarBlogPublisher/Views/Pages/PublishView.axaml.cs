using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class PublishView : UserControl {
    public PublishView() {
        InitializeComponent();
        DataContextChanged += (_, _) => HookViewModel();
    }

    private void HookViewModel() {
        if (DataContext is not PublishViewModel vm) return;
        vm.StackPageRequested -= OnStackPageRequested;
        vm.StackPageRequested += OnStackPageRequested;
    }

    private void OnStackPageRequested(object? page) {
        if (page is null) {
            PublishStack.Content = EditorRoot;
            return;
        }

        var locator = new ViewLocator();
        PublishStack.Content = locator.Build(page) ?? page;
        if (PublishStack.Content is Control c)
            c.DataContext = page;
    }
}
