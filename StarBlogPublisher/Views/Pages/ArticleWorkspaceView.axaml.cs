using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class ArticleWorkspaceView : UserControl {
    public ArticleWorkspaceView() {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
    }
    private async void OnCloseTab(object? sender, RoutedEventArgs e) {
        e.Handled = true;
        if (DataContext is ArticleWorkspaceViewModel workspace && sender is Button { DataContext: PublishViewModel document })
            await workspace.CloseDocumentCommand.ExecuteAsync(document);
    }
    private async void OnRecentClick(object? sender, RoutedEventArgs e) {
        if (DataContext is ArticleWorkspaceViewModel workspace && sender is Button { DataContext: RecentArticle article })
            await workspace.OpenPathAsync(article.Path);
    }
    private void OnOutlineClick(object? sender, RoutedEventArgs e) {
        if (DataContext is ArticleWorkspaceViewModel workspace && sender is Button { DataContext: ArticleHeading heading })
            workspace.CurrentDocument.NavigateToHeadingCommand.Execute(heading);
    }
    private void OnTaskActivate(object? sender, RoutedEventArgs e) {
        if (DataContext is ArticleWorkspaceViewModel workspace && sender is Button { DataContext: PublishViewModel document }) workspace.ActiveDocument = document;
    }
    private void OnTaskResult(object? sender, RoutedEventArgs e) {
        OnTaskActivate(sender, e);
        if (sender is Button { DataContext: PublishViewModel document }) document.ShowPublishResultCommand.Execute(null);
    }
    private async void OnTabMenuClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not ArticleWorkspaceViewModel workspace || sender is not MenuItem { DataContext: PublishViewModel document } item) return;
        switch (item.Tag as string) {
            case "saveAs": await document.SaveDocumentAsync(true); break;
            case "close": await workspace.CloseDocumentCommand.ExecuteAsync(document); break;
            case "others": await workspace.CloseOthersCommand.ExecuteAsync(document); break;
            case "right": await workspace.CloseRightCommand.ExecuteAsync(document); break;
            case "path":
                if (document.CurrentFilePath != null && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(document.CurrentFilePath);
                break;
        }
    }
    private async void OnDrop(object? sender, DragEventArgs e) {
        if (DataContext is not ArticleWorkspaceViewModel workspace) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        e.Handled = true;
        foreach (var file in files) {
            var path = file.TryGetLocalPath();
            if (path != null && (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)))
                await workspace.OpenPathAsync(path);
        }
    }
}
