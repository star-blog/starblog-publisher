using System;
using Avalonia.Input;
using FluentAvalonia.UI.Windowing;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class MainWindow : FAAppWindow {
    public MainWindow() {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        Opened += (_, _) => {
            GuiHost.SetFeedbackBar(FeedbackBar);
            SyncTitleBarMetrics();
        };
        LayoutUpdated += (_, _) => SyncTitleBarMetrics();
    }

    private void SyncTitleBarMetrics() {
        if (DataContext is MainWindowViewModel vm) {
            vm.UpdateTitleBarMetrics(TitleBar.Height, 0);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            BeginMoveDrag(e);
        }
    }
}
