using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.Services;

/// <summary>
/// GUI feedback host: shell-level Fluent InfoBar plus Fluent dialogs owned by the main window.
/// </summary>
public static class GuiHost {
    private static InfoBar? _feedbackBar;
    private static DispatcherTimer? _feedbackTimer;

    public static TopLevel? GetTopLevel() {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
    }

    public static Window? GetMainWindow() => GetTopLevel() as Window;

    /// <summary>Registers the Fluent feedback surface after the main window has opened.</summary>
    public static void SetFeedbackBar(InfoBar feedbackBar) => _feedbackBar = feedbackBar;

    public static void ToastInfo(string title, string content) =>
        ShowFeedback(title, content, InfoBarSeverity.Informational, TimeSpan.FromSeconds(4));

    public static void ToastSuccess(string title, string content) =>
        ShowFeedback(title, content, InfoBarSeverity.Success, TimeSpan.FromSeconds(4));

    public static void ToastWarning(string title, string content) =>
        ShowFeedback(title, content, InfoBarSeverity.Warning, TimeSpan.FromSeconds(5));

    public static void ToastError(string title, string content) =>
        ShowFeedback(title, content, InfoBarSeverity.Error, TimeSpan.FromSeconds(7));

    private static void ShowFeedback(string title, string content, InfoBarSeverity severity, TimeSpan duration) {
        Dispatcher.UIThread.Post(() => {
            if (_feedbackBar == null) return;

            _feedbackTimer?.Stop();
            _feedbackBar.Title = title;
            _feedbackBar.Message = content;
            _feedbackBar.Severity = severity;
            _feedbackBar.IsOpen = true;

            _feedbackTimer ??= new DispatcherTimer();
            _feedbackTimer.Interval = duration;
            _feedbackTimer.Tick -= CloseFeedback;
            _feedbackTimer.Tick += CloseFeedback;
            _feedbackTimer.Start();
        });
    }

    private static void CloseFeedback(object? sender, EventArgs e) {
        _feedbackTimer?.Stop();
        if (_feedbackBar != null) _feedbackBar.IsOpen = false;
    }

    public static async Task AlertAsync(string title, string message, NotificationType type = NotificationType.Information) {
        var dialog = CreateTaskDialog(title, message);
        dialog.Buttons.Add(new TaskDialogButton("确定", TaskDialogStandardResult.OK) { IsDefault = true });
        await dialog.ShowAsync();
    }

    public static async Task<bool> ConfirmAsync(string title, string message) {
        var dialog = CreateTaskDialog(title, message);
        dialog.Buttons.Add(new TaskDialogButton("确定", TaskDialogStandardResult.OK) { IsDefault = true });
        dialog.Buttons.Add(new TaskDialogButton("取消", TaskDialogStandardResult.Cancel));
        var result = await dialog.ShowAsync();
        return result is TaskDialogStandardResult.OK;
    }

    public static async Task<string?> PromptAsync(string title, string defaultText = "", string watermark = "") {
        var textBox = new TextBox {
            Text = defaultText,
            Watermark = watermark,
            MinWidth = 320
        };
        var dialog = new ContentDialog {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync(GetMainWindow());
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    private static TaskDialog CreateTaskDialog(string title, string message) => new() {
        Title = title,
        Header = title,
        SubHeader = message,
        XamlRoot = GetMainWindow()
    };

    public static async Task ShowContentAsync(object viewModel, string? title = null, string closeText = "关闭") {
        var locator = new StarBlogPublisher.ViewLocator();
        var content = locator.Build(viewModel) ?? new TextBlock { Text = viewModel.GetType().Name };
        if (content is Control control) {
            control.DataContext = viewModel;
        }

        var dialog = new ContentDialog {
            Title = title,
            Content = new ScrollViewer {
                Content = content,
                MaxHeight = 640,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            MaxWidth = 720,
            MaxHeight = 760,
            PrimaryButtonText = viewModel is IDialogHostAware ? null : closeText,
            CloseButtonText = viewModel is IDialogHostAware ? "取消" : null,
            DefaultButton = ContentDialogButton.Primary
        };

        if (viewModel is IDialogHostAware aware) {
            aware.CloseRequested += () => dialog.Hide();
        }

        await dialog.ShowAsync(GetMainWindow());
    }
}

/// <summary>Allows a content-dialog view model to close its hosting dialog.</summary>
public interface IDialogHostAware {
    event Action? CloseRequested;
}
