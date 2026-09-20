using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher.DesktopTests;

internal static class Program {
    private static readonly string[] Names = ["workspace-state", "sidebar-and-menus", "focus-and-palette", "preview-navigation", "settings-layout", "theme-and-close"];
    private sealed record Result(string Name, string Status, long DurationMs, string? Error = null);

    [STAThread]
    public static int Main(string[] args) {
        if (args.Contains("--help")) {
            Console.WriteLine("DesktopTests [--webview] [--settings] [--list]. Windows desktop required. Reports: output/desktop-tests/<run>/report.json");
            return 0;
        }
        if (args.Contains("--list")) { foreach (var name in Names) Console.WriteLine(name); return 0; }
        if (args.Any(a => a != "--webview" && a != "--settings")) { Console.Error.WriteLine("Unknown argument; use --help."); return 2; }
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Desktop tests currently require Windows."); return 2; }
        var output = Path.GetFullPath(Path.Combine("output", "desktop-tests", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(output);
        // Must be set before AppSettings or any application services are initialized.
        Environment.SetEnvironmentVariable("STARBLOGPUBLISHER_SETTINGS_PATH", Path.Combine(output, "settings.json"));
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(output, "webview2"));
        Console.WriteLine($"Artifacts: {output}");
        var results = new List<Result>();
        void Report() => File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        // A watchdog covers UI-thread deadlocks which an awaited task timeout cannot interrupt.
        using var watchdog = new System.Threading.Timer(_ => {
            File.WriteAllText(Path.Combine(output, "timeout.txt"), "Desktop test process exceeded 180 seconds.");
            Environment.Exit(1);
        }, null, TimeSpan.FromSeconds(180), Timeout.InfiniteTimeSpan);
        try {
            var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithLifetime(lifetime);
            var window = (MainWindow)lifetime.MainWindow!;
            var shell = (MainWindowViewModel)window.DataContext!;
            window.Width = 1280; window.Height = 800;
            window.Show();
            var scenarios = new WorkspaceScenarios(window, shell, output);
            Func<Task>[] actions = [scenarios.WorkspaceState, scenarios.SidebarAndMenus, scenarios.FocusAndPalette, scenarios.PreviewNavigation, scenarios.SettingsLayout, scenarios.ThemeAndClose];
            Dispatcher.UIThread.Post(async () => {
                var failed = false;
                for (var i = 0; i < Names.Length; i++) {
                    if (args.Contains("--settings") && Names[i] != "settings-layout") {
                        results.Add(new(Names[i], "skipped", 0, "Settings-only run"));
                        Report();
                        continue;
                    }
                    if (failed || (Names[i] == "preview-navigation" && !args.Contains("--webview"))) {
                        results.Add(new(Names[i], "skipped", 0, failed ? "Earlier workflow stage failed" : "Requires --webview"));
                        Report();
                        continue;
                    }
                    var watch = Stopwatch.StartNew();
                    try {
                        await actions[i]().WaitAsync(TimeSpan.FromSeconds(40));
                        results.Add(new(Names[i], "passed", watch.ElapsedMilliseconds));
                        Console.WriteLine($"PASS {Names[i]}");
                    }
                    catch (Exception ex) {
                        failed = true;
                        results.Add(new(Names[i], "failed", watch.ElapsedMilliseconds, ex.ToString()));
                        Console.Error.WriteLine($"FAIL {Names[i]}: {ex}");
                        try {
                            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                            bitmap.Render(window);
                            bitmap.Save(Path.Combine(output, $"failure-{Names[i]}.png"), PngBitmapEncoderOptions.Default);
                        } catch (Exception captureError) { File.WriteAllText(Path.Combine(output, "capture-error.txt"), captureError.ToString()); }
                    }
                    Report();
                }
                // Bypass unsaved-document prompts in the disposable test process.
                Environment.Exit(failed ? 1 : 0);
            });
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
            return 1;
        }
        catch (Exception ex) {
            results.Add(new("startup", "failed", 0, ex.ToString()));
            Report();
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
