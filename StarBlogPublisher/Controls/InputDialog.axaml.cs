using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace StarBlogPublisher.Controls;

public partial class InputDialog : UserControl
{
    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }
    
    public string Text
    {
        get => InputTextBox.Text ?? string.Empty;
        set => InputTextBox.Text = value;
    }
    
    public string Watermark
    {
        get => InputTextBox.Watermark ?? string.Empty;
        set => InputTextBox.Watermark = value;
    }
    
    public event EventHandler<InputDialogResult>? DialogClosed;
    
    public InputDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        TitleText = this.FindControl<TextBlock>("TitleText");
        InputTextBox = this.FindControl<TextBox>("InputTextBox");
        CancelButton = this.FindControl<Button>("CancelButton");
        OkButton = this.FindControl<Button>("OkButton");
    }
    
    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, new InputDialogResult(true, InputTextBox.Text ?? string.Empty));
    }
    
    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, new InputDialogResult(false, string.Empty));
    }
}

public class InputDialogResult
{
    public bool IsConfirmed { get; }
    public string Text { get; }
    
    public InputDialogResult(bool isConfirmed, string text)
    {
        IsConfirmed = isConfirmed;
        Text = text;
    }
} 