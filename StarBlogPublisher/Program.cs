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
            .UsePlatformDetect();

#if DEBUG
        appBuilder = appBuilder.LogToTrace();
#endif

        if (!OperatingSystem.IsWindows()) {
            appBuilder = appBuilder.WithInterFont()
                .With(new FontManagerOptions {
                    DefaultFamilyName = "fonts:Inter#Inter"
                });
        }

        StartupLog.Mark("avalonia_builder_ready");
        return appBuilder;
    }
}
