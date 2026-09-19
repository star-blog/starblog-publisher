using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class MainWindow : FAAppWindow {
    private bool _allowClose;
    private bool _checkingClose;
    public MainWindow() {
        InitializeComponent();
        TitleBar.ExtendsContentIntoTitleBar = true;
        Opened += (_, _) => {
            GuiHost.SetFeedbackBar(FeedbackBar);
            SyncTitleBarMetrics();
        };
        LayoutUpdated += (_, _) => SyncTitleBarMetrics();
        Closing += OnClosing;
        AddHandler(KeyDownEvent, OnWorkspaceKeyDown, RoutingStrategies.Tunnel);
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e) {
        if (_allowClose || DataContext is not MainWindowViewModel vm) return;
        e.Cancel = true;
        if (_checkingClose) return;
        _checkingClose = true;
        try {
            if (await vm.Workspace.CanCloseAllAsync()) { _allowClose = true; Close(); }
        }
        finally { _checkingClose = false; }
    }

    private void OnWorkspaceKeyDown(object? sender, KeyEventArgs e) {
        if (e.Source is Visual source && source.GetVisualAncestors().Any(ancestor => ancestor is FAContentDialog)) return;
        if (DataContext is not MainWindowViewModel vm || e.KeyModifiers != KeyModifiers.Control) return;
        if (e.Key == Key.O) { vm.ActivePage = vm.Workspace; vm.Workspace.OpenFilesCommand.Execute(null); }
        else if (vm.ActivePage != vm.Workspace) return;
        else if (e.Key == Key.N) vm.Workspace.NewDocumentCommand.Execute(null);
        else if (e.Key == Key.S) vm.Workspace.SaveActiveCommand.Execute(null);
        else if (e.Key == Key.W) vm.Workspace.CloseActiveCommand.Execute(null);
        else if (e.Key == Key.Tab) vm.Workspace.NextDocumentCommand.Execute(null);
        else if (e.Key == Key.B) vm.Workspace.ToggleSidebarCommand.Execute(null);
        else return;
        e.Handled = true;
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
