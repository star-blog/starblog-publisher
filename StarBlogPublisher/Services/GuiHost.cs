using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.Services;

/// <summary>
/// GUI feedback host: shell-level Fluent FAInfoBar plus Fluent dialogs owned by the main window.
/// </summary>
public static class GuiHost {
    private static FAInfoBar? _feedbackBar;
    private static DispatcherTimer? _feedbackTimer;
    private static Button? _copyButton;
    private static DispatcherTimer? _copyButtonResetTimer;
    private static string _pendingCopyText = string.Empty;

    public static TopLevel? GetTopLevel() {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
    }

    public static Window? GetMainWindow() => GetTopLevel() as Window;

    /// <summary>Registers the Fluent feedback surface after the main window has opened.</summary>
    public static void SetFeedbackBar(FAInfoBar feedbackBar) {
        _feedbackBar = feedbackBar;
        EnsureCopyActionButton(feedbackBar);
    }

    public static void ToastInfo(string title, string content) =>
        ShowFeedback(title, content, FAInfoBarSeverity.Informational, TimeSpan.FromSeconds(4));

    public static void ToastSuccess(string title, string content) =>
        ShowFeedback(title, content, FAInfoBarSeverity.Success, TimeSpan.FromSeconds(4));

    public static void ToastWarning(string title, string content) =>
        ShowFeedback(title, content, FAInfoBarSeverity.Warning, TimeSpan.FromSeconds(5));

    public static void ToastError(string title, string content) =>
        ShowFeedback(title, content, FAInfoBarSeverity.Error, TimeSpan.FromSeconds(12));

    private static void ShowFeedback(string title, string content, FAInfoBarSeverity severity, TimeSpan duration) {
        Dispatcher.UIThread.Post(() => {
            if (_feedbackBar == null) return;

            EnsureCopyActionButton(_feedbackBar);
            _feedbackTimer?.Stop();
            _copyButtonResetTimer?.Stop();
            if (_copyButton != null) _copyButton.Content = "复制";

            _pendingCopyText = string.IsNullOrWhiteSpace(title)
                ? content
                : string.IsNullOrWhiteSpace(content) ? title : $"{title}\n{content}";
            _feedbackBar.Title = title;
            _feedbackBar.Message = content;
            _feedbackBar.Severity = severity;
            _feedbackBar.IsOpen = true;

            // Errors stay longer so the copy action is usable before auto-dismiss.
            _feedbackTimer ??= new DispatcherTimer();
            _feedbackTimer.Interval = duration;
            _feedbackTimer.Tick -= CloseFeedback;
            _feedbackTimer.Tick += CloseFeedback;
            _feedbackTimer.Start();
        });
    }

    private static void EnsureCopyActionButton(FAInfoBar feedbackBar) {
        if (_copyButton != null) {
            feedbackBar.ActionButton = _copyButton;
            return;
        }

        _copyButton = new Button {
            Content = "复制",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 64
        };
        _copyButton.Click += async (_, _) => await CopyFeedbackAsync();
        feedbackBar.ActionButton = _copyButton;
    }

    private static async Task CopyFeedbackAsync() {
        var clipboard = GetTopLevel()?.Clipboard;
        if (clipboard == null || string.IsNullOrWhiteSpace(_pendingCopyText)) return;

        await clipboard.SetTextAsync(_pendingCopyText);
        if (_copyButton != null) {
            _copyButton.Content = "已复制";
            _copyButtonResetTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _copyButtonResetTimer.Tick -= ResetCopyButtonLabel;
            _copyButtonResetTimer.Tick += ResetCopyButtonLabel;
            _copyButtonResetTimer.Stop();
            _copyButtonResetTimer.Start();
        }
    }

    private static void ResetCopyButtonLabel(object? sender, EventArgs e) {
        _copyButtonResetTimer?.Stop();
        if (_copyButton != null) _copyButton.Content = "复制";
    }

    private static void CloseFeedback(object? sender, EventArgs e) {
        _feedbackTimer?.Stop();
        if (_feedbackBar != null) _feedbackBar.IsOpen = false;
    }

    public static async Task AlertAsync(string title, string message, NotificationType type = NotificationType.Information) {
        var dialog = CreateTaskDialog(title, message);
        dialog.Buttons.Add(new FATaskDialogButton("确定", FATaskDialogStandardResult.OK) { IsDefault = true });
        await dialog.ShowAsync();
    }

    public static async Task<bool> ConfirmAsync(string title, string message) {
        var dialog = CreateTaskDialog(title, message);
        dialog.Buttons.Add(new FATaskDialogButton("确定", FATaskDialogStandardResult.OK) { IsDefault = true });
        dialog.Buttons.Add(new FATaskDialogButton("取消", FATaskDialogStandardResult.Cancel));
        var result = await dialog.ShowAsync();
        return result is FATaskDialogStandardResult.OK;
    }

    public static async Task<string?> PromptAsync(string title, string defaultText = "", string watermark = "") {
        var textBox = new TextBox {
            Text = defaultText,
            PlaceholderText = watermark,
            MinWidth = 320
        };
        var dialog = new FAContentDialog {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync(GetMainWindow());
        return result == FAContentDialogResult.Primary ? textBox.Text : null;
    }

    private static FATaskDialog CreateTaskDialog(string title, string message) => new() {
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

        var dialog = new FAContentDialog {
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
            DefaultButton = FAContentDialogButton.Primary
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
