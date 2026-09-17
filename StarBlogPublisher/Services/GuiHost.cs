using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.Services;

/// <summary>
/// GUI 宿主服务：Toast / Dialog / 主窗口 TopLevel。
/// </summary>
public static class GuiHost {
    private static WindowNotificationManager? _notifications;

    public static TopLevel? GetTopLevel() {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
    }

    public static Window? GetMainWindow() => GetTopLevel() as Window;

    public static void EnsureNotifications(Visual visual) {
        _notifications ??= new WindowNotificationManager(TopLevel.GetTopLevel(visual)) {
            Position = NotificationPosition.TopRight,
            MaxItems = 5
        };
    }

    private static WindowNotificationManager? Notifications {
        get {
            if (_notifications != null) return _notifications;
            var top = GetTopLevel();
            if (top == null) return null;
            _notifications = new WindowNotificationManager(top) {
                Position = NotificationPosition.TopRight,
                MaxItems = 5
            };
            return _notifications;
        }
    }

    public static void ToastInfo(string title, string content) =>
        Notifications?.Show(new Notification(title, content, NotificationType.Information, TimeSpan.FromSeconds(3)));

    public static void ToastSuccess(string title, string content) =>
        Notifications?.Show(new Notification(title, content, NotificationType.Success, TimeSpan.FromSeconds(3)));

    public static void ToastWarning(string title, string content) =>
        Notifications?.Show(new Notification(title, content, NotificationType.Warning, TimeSpan.FromSeconds(4)));

    public static void ToastError(string title, string content) =>
        Notifications?.Show(new Notification(title, content, NotificationType.Error, TimeSpan.FromSeconds(5)));

    public static async Task AlertAsync(string title, string message, NotificationType type = NotificationType.Information) {
        var dialog = new ContentDialog {
            Title = title,
            Content = message,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary
        };
        await dialog.ShowAsync(GetMainWindow());
    }

    public static async Task<bool> ConfirmAsync(string title, string message) {
        var dialog = new ContentDialog {
            Title = title,
            Content = message,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync(GetMainWindow());
        return result == ContentDialogResult.Primary;
    }

    public static async Task<string?> PromptAsync(string title, string defaultText = "", string watermark = "") {
        var textBox = new TextBox {
            Text = defaultText,
            Watermark = watermark,
            MinWidth = 280
        };
        var dialog = new ContentDialog {
            Title = title,
            Content = textBox,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await dialog.ShowAsync(GetMainWindow());
        return result == ContentDialogResult.Primary ? textBox.Text : null;
    }

    public static async Task ShowContentAsync(object viewModel, string? title = null, string closeText = "关闭") {
        var locator = new StarBlogPublisher.ViewLocator();
        var content = locator.Build(viewModel) ?? new TextBlock { Text = viewModel.GetType().Name };
        if (content is Control control) {
            control.DataContext = viewModel;
        }

        var dialog = new ContentDialog {
            Title = title,
            Content = content,
            PrimaryButtonText = closeText,
            DefaultButton = ContentDialogButton.Primary
        };

        if (viewModel is IDialogHostAware aware) {
            aware.CloseRequested += () => dialog.Hide();
        }

        await dialog.ShowAsync(GetMainWindow());
    }
}

/// <summary>
/// 允许 ViewModel 主动关闭 ContentDialog（替代 ISukiDialog.Dismiss）。
/// </summary>
public interface IDialogHostAware {
    event Action? CloseRequested;
}
