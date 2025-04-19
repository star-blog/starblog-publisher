using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class AddCategoryWindow : Window {
    public AddCategoryWindow() {
        InitializeComponent();
        var vm = new AddCategoryWindowViewModel {
            View = this
        };
        DataContext = vm;
    }
}