using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Editor;

public sealed class MarkdownSyntaxColorizer : DocumentColorizingTransformer {
    private readonly IBrush _heading;
    private readonly IBrush _bold;
    private readonly IBrush _inlineCode;
    private readonly IBrush _fencedCode;
    private readonly IBrush _link;
    private readonly IBrush _quote;
    private readonly Typeface _headingTypeface;
    private readonly Typeface _boldTypeface;
    private readonly Typeface _quoteTypeface;

    private static readonly FontFamily EditorFont = new("Cascadia Code, Consolas, Menlo, monospace");

    public MarkdownSyntaxColorizer(bool isDark) {
        if (isDark) {
            _heading = Brush("#79C0FF");
            _bold = Brush("#E6EDF3");
            _inlineCode = Brush("#FFA657");
            _fencedCode = Brush("#8B949E");
            _link = Brush("#58A6FF");
            _quote = Brush("#8B949E");
        }
        else {
            _heading = Brush("#0550AE");
            _bold = Brush("#1F2328");
            _inlineCode = Brush("#CF222E");
            _fencedCode = Brush("#656D76");
            _link = Brush("#0969DA");
            _quote = Brush("#656D76");
        }

        _headingTypeface = new Typeface(EditorFont, FontStyle.Normal, FontWeight.SemiBold);
        _boldTypeface = new Typeface(EditorFont, FontStyle.Normal, FontWeight.Bold);
        _quoteTypeface = new Typeface(EditorFont, FontStyle.Italic, FontWeight.Normal);
    }

    protected override void ColorizeLine(DocumentLine line) {
        var document = CurrentContext.Document;
        var inFence = false;
        for (var current = document.GetLineByNumber(1);
             current != null && current.LineNumber < line.LineNumber;
             current = current.NextLine) {
            if (MarkdownHighlightTokenizer.IsFenceDelimiter(document.GetText(current))) {
                inFence = !inFence;
            }
        }

        var text = document.GetText(line);
        foreach (var span in MarkdownHighlightTokenizer.Tokenize(text, inFence)) {
            var start = line.Offset + span.Start;
            var end = start + span.Length;
            if (span.Length <= 0 || start < line.Offset || end > line.EndOffset) {
                continue;
            }

            ChangeLinePart(start, end, element => Apply(element, span.Kind));
        }
    }

    private void Apply(VisualLineElement element, MarkdownHighlightKind kind) {
        var properties = element.TextRunProperties;
        switch (kind) {
            case MarkdownHighlightKind.Heading:
                properties.SetForegroundBrush(_heading);
                properties.SetTypeface(_headingTypeface);
                break;
            case MarkdownHighlightKind.Bold:
                properties.SetForegroundBrush(_bold);
                properties.SetTypeface(_boldTypeface);
                break;
            case MarkdownHighlightKind.InlineCode:
                properties.SetForegroundBrush(_inlineCode);
                break;
            case MarkdownHighlightKind.FencedCode:
                properties.SetForegroundBrush(_fencedCode);
                break;
            case MarkdownHighlightKind.Link:
                properties.SetForegroundBrush(_link);
                break;
            case MarkdownHighlightKind.Quote:
                properties.SetForegroundBrush(_quote);
                properties.SetTypeface(_quoteTypeface);
                break;
        }
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
