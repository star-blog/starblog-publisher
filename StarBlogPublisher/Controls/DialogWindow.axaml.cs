using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StarBlogPublisher.Controls;

public partial class DialogWindow : Window
{
    private readonly ContentControl _contentContainer;
    
    public DialogWindow()
    {
        InitializeComponent();
        _contentContainer = this.FindControl<ContentControl>("ContentContainer");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public static async Task<InputDialogResult> ShowInputDialog(Window owner, string title, string defaultText = "", string watermark = "")
    {
        var dialog = new DialogWindow
        {
            Title = title
        };
        
        var inputDialog = new InputDialog
        {
            Title = title,
            Text = defaultText,
            Watermark = watermark
        };
        
        var taskCompletionSource = new TaskCompletionSource<InputDialogResult>();
        
        inputDialog.DialogClosed += (sender, args) =>
        {
            taskCompletionSource.SetResult(args);
            dialog.Close();
        };
        
        dialog._contentContainer.Content = inputDialog;
        
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        await dialog.ShowDialog(owner);
        
        return await taskCompletionSource.Task;
    }
    
    public static async Task<bool> ShowConfirmDialog(Window owner, string title, string message)
    {
        var dialog = new DialogWindow
        {
            Title = title
        };
        
        var confirmDialog = new ConfirmDialog
        {
            Title = title,
            Message = message
        };
        
        var taskCompletionSource = new TaskCompletionSource<bool>();
        
        confirmDialog.DialogClosed += (sender, confirmed) =>
        {
            taskCompletionSource.SetResult(confirmed);
            dialog.Close();
        };
        
        dialog._contentContainer.Content = confirmDialog;
        
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        await dialog.ShowDialog(owner);
        
        return await taskCompletionSource.Task;
    }
} 