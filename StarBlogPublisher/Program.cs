using Avalonia;
using Avalonia.Media;
using System;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace StarBlogPublisher;

sealed class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            // Some Linux font backends can return an empty system default font.
            // Use the Inter collection shipped with the app instead of $Default.
            .With(new FontManagerOptions {
                DefaultFamilyName = "fonts:Inter#Inter"
            })
            .LogToTrace();
    }
}
