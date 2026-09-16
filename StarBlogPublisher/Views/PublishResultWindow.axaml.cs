using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class PublishResultWindow : Window {
    public PublishResultWindow() {
        InitializeComponent();
    }

    public PublishResultWindow(PublishResult result) : this() {
        var viewModel = new PublishResultWindowViewModel(result);
        viewModel.SetWindow(this);
        DataContext = viewModel;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
