using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class WordCloudWindow : Window {
    public WordCloudWindow() {
        InitializeComponent();
        DataContext = new WordCloudWindowViewModel();
    }
}