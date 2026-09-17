using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace StarBlogPublisher.Services;

/// <summary>
/// GUI 宿主服务：Toast / Dialog / 主窗口 TopLevel。替代 App.MainWindow 静态引用。
/// </summary>
public static class GuiHost {
    public static ISukiToastManager Toasts { get; } = new SukiToastManager();
    public static ISukiDialogManager Dialogs { get; } = new SukiDialogManager();

    public static TopLevel? GetTopLevel() {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window
            : null;
    }

    public static Window? GetMainWindow() => GetTopLevel() as Window;

    public static void ToastInfo(string title, string content) =>
        Toasts.CreateToast()
            .WithTitle(title)
            .WithContent(content)
            .OfType(NotificationType.Information)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Dismiss().ByClicking()
            .Queue();

    public static void ToastSuccess(string title, string content) =>
        Toasts.CreateToast()
            .WithTitle(title)
            .WithContent(content)
            .OfType(NotificationType.Success)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Dismiss().ByClicking()
            .Queue();

    public static void ToastWarning(string title, string content) =>
        Toasts.CreateToast()
            .WithTitle(title)
            .WithContent(content)
            .OfType(NotificationType.Warning)
            .Dismiss().After(TimeSpan.FromSeconds(4))
            .Dismiss().ByClicking()
            .Queue();

    public static void ToastError(string title, string content) =>
        Toasts.CreateToast()
            .WithTitle(title)
            .WithContent(content)
            .OfType(NotificationType.Error)
            .Dismiss().After(TimeSpan.FromSeconds(5))
            .Dismiss().ByClicking()
            .Queue();

    public static Task AlertAsync(string title, string message, NotificationType type = NotificationType.Information) {
        return Dialogs.CreateDialog()
            .OfType(type)
            .WithTitle(title)
            .WithContent(message)
            .WithOkResult("确定")
            .TryShowAsync();
    }

    public static Task<bool> ConfirmAsync(string title, string message) {
        return Dialogs.CreateDialog()
            .OfType(NotificationType.Warning)
            .WithTitle(title)
            .WithContent(message)
            .WithYesNoResult("确定", "取消")
            .TryShowAsync();
    }

    public static async Task<string?> PromptAsync(string title, string defaultText = "", string watermark = "") {
        var textBox = new TextBox {
            Text = defaultText,
            Watermark = watermark,
            MinWidth = 280
        };
        var tcs = new TaskCompletionSource<string?>();
        Dialogs.CreateDialog()
            .WithTitle(title)
            .WithContent(textBox)
            .Dismiss().ByClickingBackground()
            .WithActionButton("取消", _ => tcs.TrySetResult(null), true)
            .WithActionButton("确定", _ => tcs.TrySetResult(textBox.Text), true, "Accent")
            .OnDismissed(_ => tcs.TrySetResult(null))
            .TryShow();
        return await tcs.Task;
    }
}
