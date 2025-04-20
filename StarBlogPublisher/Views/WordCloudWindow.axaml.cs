using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class WordCloudWindow : Window {
    public WordCloudWindow() {
        InitializeComponent();
        var vm = new WordCloudWindowViewModel {
            View = this
        };
        DataContext = vm;
    }
}