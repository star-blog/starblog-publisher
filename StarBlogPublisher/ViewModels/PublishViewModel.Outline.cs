using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace StarBlogPublisher.ViewModels;

public sealed record ArticleHeading(string Title, int Line, int Level) {
    public Thickness Indentation => new((Level - 1) * 12, 0, 0, 0);
    public string LevelLabel => $"H{Level}";
}

public partial class PublishViewModel {
    public ObservableCollection<ArticleHeading> OutlineEntries { get; } = new();
    [ObservableProperty] private int _cursorLine = 1;
    [ObservableProperty] private int _cursorColumn = 1;
    public string CursorPositionText => $"行 {CursorLine}，列 {CursorColumn}";
    public string BreadcrumbText => string.Join(" › ", new[] {
        string.IsNullOrWhiteSpace(CurrentFilePath) ? null : Path.GetFileName(Path.GetDirectoryName(CurrentFilePath)),
        DocumentFileName, OutlineEntries.LastOrDefault(h => h.Line <= CursorLine)?.Title
    }.Where(part => !string.IsNullOrWhiteSpace(part)));
    public event Action<int>? NavigateToLineRequested;

    private void UpdateOutline() {
        OutlineEntries.Clear();
        foreach (var heading in Markdig.Markdown.Parse(ArticleContent).Descendants<HeadingBlock>()) {
            var title = string.Concat(heading.Inline?.Descendants().Select(part => part switch {
                LiteralInline literal => literal.Content.ToString(), CodeInline code => code.Content,
                LineBreakInline => " ", _ => ""
            }) ?? []);
            var line = ArticleContent.AsSpan(0, Math.Clamp(heading.Span.Start, 0, ArticleContent.Length)).Count('\n') + 1;
            OutlineEntries.Add(new(title, line, heading.Level));
        }
        OnPropertyChanged(nameof(BreadcrumbText));
    }
    partial void OnCursorLineChanged(int value) { OnPropertyChanged(nameof(CursorPositionText)); OnPropertyChanged(nameof(BreadcrumbText)); }
    partial void OnCursorColumnChanged(int value) => OnPropertyChanged(nameof(CursorPositionText));

    [RelayCommand] private void NavigateToHeading(ArticleHeading heading) {
        EditorMode = MarkdownEditorMode.Source;
        NavigateToLineRequested?.Invoke(heading.Line);
    }
}
