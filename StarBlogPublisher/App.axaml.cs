using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        RefitTypeRegistration.RegisterTypes();
        _ = AppSettings.Instance;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            RequestedThemeVariant = AppSettings.Instance.IsDarkTheme
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            desktop.MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel(),
            };

            if (AppSettings.HasLoadError) {
                _ = ShowSettingsLoadErrorAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ShowSettingsLoadErrorAsync() {
        var message = AppSettings.LoadErrorMessage;
        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        await GuiHost.AlertAsync("配置加载失败", message, NotificationType.Error);
    }
}
