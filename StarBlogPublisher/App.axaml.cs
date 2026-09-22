using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
        StartupLog.Mark("xaml_loaded");
    }

    public override void OnFrameworkInitializationCompleted() {
        RefitTypeRegistration.RegisterTypes();
        _ = AppSettings.Instance;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            AppThemeService.Apply(AppSettings.Instance.ThemeMode);

            StartupLog.Mark("before_shell");
            desktop.MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel(),
            };
            StartupLog.Mark("after_shell");

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
