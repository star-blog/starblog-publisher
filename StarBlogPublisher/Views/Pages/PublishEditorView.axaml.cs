using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class PublishEditorView : UserControl {
    public string Title => "编辑";

    public PublishEditorView() {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private void OnDragOver(object? sender, DragEventArgs e) {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e) {
        if (DataContext is not PublishViewModel vm) return;
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault();
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        await vm.LoadFromPathAsync(path);
    }
}
