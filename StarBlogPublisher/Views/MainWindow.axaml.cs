using System;
using FluentAvalonia.UI.Windowing;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class MainWindow : AppWindow {
    public MainWindow() {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;
        Opened += (_, _) => {
            GuiHost.SetFeedbackBar(FeedbackBar);
            SyncTitleBarMetrics();
        };
        LayoutUpdated += (_, _) => SyncTitleBarMetrics();
    }

    private void SyncTitleBarMetrics() {
        if (DataContext is MainWindowViewModel vm) {
            vm.UpdateTitleBarMetrics(TitleBar.Height);
        }
    }
}
