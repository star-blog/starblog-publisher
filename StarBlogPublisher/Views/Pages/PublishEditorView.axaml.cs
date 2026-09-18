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
        SizeChanged += OnWorkspaceSizeChanged;
        DataContextChanged += (_, _) => {
            if (DataContext is PublishViewModel vm) {
                vm.SetWorkspaceWidth(Bounds.Width);
            }
        };
    }

    private void OnWorkspaceSizeChanged(object? sender, SizeChangedEventArgs e) {
        if (DataContext is PublishViewModel vm) {
            vm.SetWorkspaceWidth(e.NewSize.Width);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e) {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e) {
        if (DataContext is not PublishViewModel vm) return;
        if (!e.Data.Contains(DataFormats.Files)) return;
        var file = e.Data.GetFiles()?.FirstOrDefault();
        var path = file?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".md", System.StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        await vm.LoadFromPathAsync(path);
    }
}
