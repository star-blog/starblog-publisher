using Avalonia.Controls;
using Avalonia.Input;

namespace StarBlogPublisher.Views;

public partial class SettingsView : UserControl {
    public SettingsView() {
        InitializeComponent();
    }

    private void AutoCompleteBox_GotFocus(object? sender, GotFocusEventArgs e) {
        if (sender is AutoCompleteBox autoCompleteBox) {
            autoCompleteBox.IsDropDownOpen = true;
        }
    }
}
