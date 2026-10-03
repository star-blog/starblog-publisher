using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using AvaloniaEdit.Document;

namespace StarBlogPublisher.ViewModels;

// The shared editor needs document state, not publishing or local-file operations.
public interface IMarkdownEditorContext : INotifyPropertyChanged, INotifyPropertyChanging {
    string ArticleContent { get; set; }
    TextDocument EditorDocument { get; }
    MarkdownEditorMode EditorMode { get; set; }
    bool CanPreview { get; }
    bool IsSourceMode { get; set; }
    bool IsSplitMode { get; set; }
    bool IsPreviewMode { get; set; }
    bool IsSourcePaneVisible { get; }
    bool IsPreviewPaneVisible { get; }
    GridLength SourceColumnWidth { get; }
    GridLength PreviewColumnWidth { get; }
    GridLength SplitterColumnWidth { get; }
    Uri? PreviewUri { get; }
    double EditorFontSize { get; set; }
    bool EditorWordWrap { get; set; }
    bool EditorShowLineNumbers { get; set; }
    int CaretOffset { get; set; }
    double VerticalOffset { get; set; }
    double HorizontalOffset { get; set; }
    int CursorLine { get; set; }
    int CursorColumn { get; set; }
    ICommand SetEditorModeCommand { get; }
    ICommand IncreaseEditorFontSizeCommand { get; }
    ICommand DecreaseEditorFontSizeCommand { get; }
    event Action<int>? NavigateToLineRequested;
    event Action<int>? NavigatePreviewToLineRequested;
}
