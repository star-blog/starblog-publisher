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
        PublishStack.Content = page ?? EditorRoot;
    }
}
