using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace StarBlogPublisher.Controls;

public partial class ConfirmDialog : UserControl
{
    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }
    
    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set => MessageText.Text = value;
    }
    
    public event EventHandler<bool>? DialogClosed;
    
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        TitleText = this.FindControl<TextBlock>("TitleText");
        MessageText = this.FindControl<TextBlock>("MessageText");
        CancelButton = this.FindControl<Button>("CancelButton");
        OkButton = this.FindControl<Button>("OkButton");
    }
    
    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
    
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
} 