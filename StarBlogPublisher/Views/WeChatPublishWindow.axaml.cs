using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class WeChatPublishWindow : Window {
    public WeChatPublishWindow() {
        InitializeComponent();
    }

    public WeChatPublishWindow(string markdown, string sourceFilePath, string title, string summary) : this() {
        DataContext = new WeChatPublishWindowViewModel(markdown, sourceFilePath, title, summary);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
