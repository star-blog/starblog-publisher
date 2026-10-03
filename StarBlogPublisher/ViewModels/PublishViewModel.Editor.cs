using System.Windows.Input;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel : IMarkdownEditorContext {
    ICommand IMarkdownEditorContext.SetEditorModeCommand => SetEditorModeCommand;
    ICommand IMarkdownEditorContext.IncreaseEditorFontSizeCommand => IncreaseEditorFontSizeCommand;
    ICommand IMarkdownEditorContext.DecreaseEditorFontSizeCommand => DecreaseEditorFontSizeCommand;
}
