using System;
using Avalonia.Controls;
using Avalonia.Input;
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
