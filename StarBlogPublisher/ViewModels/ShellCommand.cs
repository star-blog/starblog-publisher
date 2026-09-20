using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

/// <summary>One command definition shared by menus, keyboard shortcuts and the command palette.</summary>
public sealed partial class ShellCommand : ObservableObject {
    public string Id { get; }
    public string Group { get; }
    public string Label { get; }
    public string? Shortcut { get; }
    public IAsyncRelayCommand Command { get; }
    private readonly Func<bool>? _checked;
    public bool IsToggle => _checked != null;
    public bool IsChecked => _checked?.Invoke() == true;

    public ShellCommand(string id, string group, string label, string? shortcut, Func<Task> execute,
        Func<bool>? canExecute = null, Func<bool>? isChecked = null) {
        Id = id; Group = group; Label = label; Shortcut = shortcut; _checked = isChecked;
        Command = new AsyncRelayCommand(async () => {
            try { await execute(); }
            catch (Exception ex) { GuiHost.ToastError(label, ex.Message); }
        }, canExecute ?? (() => true));
    }

    public void Refresh() {
        Command.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsChecked));
    }
}
