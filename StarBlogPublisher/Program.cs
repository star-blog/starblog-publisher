using Avalonia;
using Avalonia.Media;
using System;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;
using StarBlogPublisher.Services;

namespace StarBlogPublisher;

sealed class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
        StartupLog.Mark("main_enter");
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        var appBuilder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // FluentAvalonia's Windows controls are calibrated for Segoe UI / Segoe UI
        // Variable Text. Keep that native typography on Windows; only use the bundled
        // Inter font as a safe fallback for platforms whose system font lookup is unreliable.
        if (!OperatingSystem.IsWindows()) {
            appBuilder = appBuilder.With(new FontManagerOptions {
                DefaultFamilyName = "fonts:Inter#Inter"
            });
        }

        StartupLog.Mark("avalonia_builder_ready");
        return appBuilder;
    }
}
