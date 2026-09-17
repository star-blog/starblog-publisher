using FluentAvalonia.UI.Windowing;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Views;

public partial class MainWindow : AppWindow {
    public MainWindow() {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        Opened += (_, _) => GuiHost.SetFeedbackBar(FeedbackBar);
    }
}
