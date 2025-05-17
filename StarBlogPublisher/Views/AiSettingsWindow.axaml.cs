using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class AiSettingsWindow : Window {
    public AiSettingsWindow() {
        InitializeComponent();
        DataContext = new AiSettingsWindowViewModel {
            View = this
        };
    }

    private void AutoCompleteBox_GotFocus(object sender, GotFocusEventArgs e) {
        // 当获取焦点时，显示下拉列表
        if (sender is AutoCompleteBox autoCompleteBox) {
            autoCompleteBox.IsDropDownOpen = true;
        }
    }
}