using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class CoverPromptWindow : Window {
    public CoverPromptWindow() {
        AvaloniaXamlLoader.Load(this);
        DataContext = new CoverPromptWindowViewModel { View = this };
    }
}