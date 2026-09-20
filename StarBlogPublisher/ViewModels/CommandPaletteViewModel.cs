using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarBlogPublisher.ViewModels;

public sealed record PaletteEntry(string Label, string Detail, Func<Task> Execute, Func<bool> CanExecute, string SearchText) {
    public string DisplayDetail => CanExecute() ? Detail : Detail + " · 当前不可用";
}

public partial class CommandPaletteViewModel : ViewModelBase {
    private readonly MainWindowViewModel _shell;
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private PaletteEntry? _selectedEntry;
    public ObservableCollection<PaletteEntry> Entries { get; } = new();
    public bool HasResults => Entries.Count > 0;

    public CommandPaletteViewModel(MainWindowViewModel shell, bool commands) {
        _shell = shell;
        _query = commands ? "> " : "";
        Refresh();
    }

    partial void OnQueryChanged(string value) => Refresh();
    public void Refresh() {
        var commands = Query.TrimStart().StartsWith('>');
        var query = commands ? Query.TrimStart()[1..].Trim() : Query.Trim();
        var candidates = new List<PaletteEntry>();
        if (commands) {
            candidates.AddRange(_shell.ShellCommands.Where(c => c.Id != "view.commands" && c.Id != "file.quickOpen")
                .Select(c => new PaletteEntry(c.Label, $"{c.Group}    {c.Shortcut}",
                    () => c.Command.ExecuteAsync(null), () => c.Command.CanExecute(null), $"{c.Label} {c.Group} {c.Id}")));
        }
        else {
            foreach (var document in _shell.Workspace.Documents) {
                candidates.Add(new(document.DocumentDisplayName, document.CurrentFilePath ?? "未保存文章", () => {
                    _shell.ActivePage = _shell.Workspace; _shell.Workspace.ActiveDocument = document;
                    return Task.CompletedTask;
                }, () => true, document.DocumentFileName + " " + document.CurrentFilePath));
            }
            foreach (var recent in _shell.Workspace.RecentFiles.Where(r => !_shell.Workspace.Documents.Any(d => string.Equals(d.CurrentFilePath, r.Path,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))) {
                candidates.Add(new(recent.FileName, recent.Path, async () => {
                    _shell.ActivePage = _shell.Workspace; await _shell.Workspace.OpenPathAsync(recent.Path);
                }, () => true, recent.FileName + " " + recent.Path));
            }
        }
        Entries.Clear();
        foreach (var entry in candidates.Where(e => e.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase))) Entries.Add(entry);
        SelectedEntry = Entries.FirstOrDefault();
        OnPropertyChanged(nameof(HasResults));
    }
}
