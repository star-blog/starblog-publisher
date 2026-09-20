using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views.Controls;

public partial class CommandPaletteView : UserControl {
    public event Action? ExecuteRequested;
    public CommandPaletteView() {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => { QueryBox.Focus(); QueryBox.CaretIndex = QueryBox.Text?.Length ?? 0; });
        AddHandler(KeyDownEvent, (_, e) => {
            if (DataContext is not CommandPaletteViewModel vm) return;
            if (e.Key == Key.Enter) { ExecuteRequested?.Invoke(); e.Handled = true; }
            else if (e.Key is Key.Down or Key.Up && vm.Entries.Count > 0) {
                var index = vm.SelectedEntry == null ? 0 : vm.Entries.IndexOf(vm.SelectedEntry);
                Results.SelectedIndex = Math.Clamp(index + (e.Key == Key.Down ? 1 : -1), 0, vm.Entries.Count - 1);
                if (Results.SelectedItem != null) Results.ScrollIntoView(Results.SelectedItem);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        Results.DoubleTapped += (_, _) => ExecuteRequested?.Invoke();
    }
}
