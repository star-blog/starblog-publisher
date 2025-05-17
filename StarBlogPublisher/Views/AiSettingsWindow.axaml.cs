using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class AiSettingsWindow : Window {
    public AiSettingsWindow() {
        InitializeComponent();
        DataContext = new AiSettingsWindowViewModel {
            View = this
        };
    }
} 