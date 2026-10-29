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
    private static readonly string[] Names = ["workspace-state", "sidebar-and-menus", "focus-and-palette", "preview-navigation", "settings-layout", "site-posts-layout", "site-posts-preview", "cover-studio", "theme-and-close", "material-shell"];
    private sealed record Result(string Name, string Status, long DurationMs, string? Error = null);

    [STAThread]
    public static int Main(string[] args) {
        if (args.Contains("--help")) {
            Console.WriteLine("DesktopTests [--webview] [--settings] [--site-posts] [--cover-studio] [--material] [--dialogs] [--list]. Windows desktop required. Reports: output/desktop-tests/<run>/report.json");
            return 0;
        }
        if (args.Contains("--list")) { foreach (var name in Names.Concat(DialogScenarios.Cases.Select(c => "dialogs-" + c))) Console.WriteLine(name); return 0; }
        if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Desktop tests currently require Windows."); return 2; }
        if (args.Length == 1 && args[0] == "--dialogs") return DialogScenarios.RunIsolatedCases().GetAwaiter().GetResult();
        var dialogCase = args.Length == 1 && args[0].StartsWith("--dialog-case=", StringComparison.Ordinal)
            ? args[0]["--dialog-case=".Length..] : null;
        if (dialogCase != null ? !DialogScenarios.Cases.Contains(dialogCase)
            : args.Any(a => a != "--webview" && a != "--settings" && a != "--site-posts" && a != "--cover-studio" && a != "--material")) {
            Console.Error.WriteLine("Unknown argument; use --help."); return 2;
        }
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
            Optris.Icons.Avalonia.IconProvider.Current.Register<Optris.Icons.Avalonia.FontAwesome.FontAwesomeIconProvider>();
            var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithLifetime(lifetime);
            var window = (MainWindow)lifetime.MainWindow!;
            var shell = (MainWindowViewModel)window.DataContext!;
            window.Width = 1280; window.Height = 800;
            window.Show();
            var scenarios = new WorkspaceScenarios(window, shell, output);
            var names = dialogCase == null ? Names : ["dialogs-" + dialogCase];
            Func<Task>[] actions = dialogCase == null
                ? [scenarios.WorkspaceState, scenarios.SidebarAndMenus, scenarios.FocusAndPalette, scenarios.PreviewNavigation, scenarios.SettingsLayout, scenarios.SitePostsLayout, scenarios.SitePostsPreview, scenarios.CoverStudio, scenarios.ThemeAndClose, scenarios.MaterialShell]
                : [() => new DialogScenarios(window, shell, output).RunAsync(dialogCase)];
            Dispatcher.UIThread.Post(async () => {
                var failed = false;
                for (var i = 0; i < names.Length; i++) {
                    var name = names[i];
                    if (args.Contains("--cover-studio") && name != "cover-studio") {
                        results.Add(new(name, "skipped", 0, "Cover-studio-only run"));
                        Report();
                        continue;
                    }
                    if (args.Contains("--site-posts") && !name.StartsWith("site-posts-")) {
                        results.Add(new(name, "skipped", 0, "Site-posts-only run"));
                        Report();
                        continue;
                    }
                    if (args.Contains("--settings") && name != "settings-layout") {
                        results.Add(new(name, "skipped", 0, "Settings-only run"));
                        Report();
                        continue;
                    }
                    if (args.Contains("--material") && name != "material-shell") {
                        results.Add(new(name, "skipped", 0, "Material-only run"));
                        Report();
                        continue;
                    }
                    if (name == "material-shell" && !args.Contains("--material")) {
                        results.Add(new(name, "skipped", 0, "Requires --material"));
                        Report();
                        continue;
                    }
                    if (failed || (name is "preview-navigation" or "site-posts-preview" && !args.Contains("--webview"))) {
                        results.Add(new(name, "skipped", 0, failed ? "Earlier workflow stage failed" : "Requires --webview"));
                        Report();
                        continue;
                    }
                    var watch = Stopwatch.StartNew();
                    try {
                        var manualCover = name == "cover-studio" && Environment.GetEnvironmentVariable("STARBLOG_COVER_MANUAL_TEST") == "1";
                        await actions[i]().WaitAsync(TimeSpan.FromSeconds(manualCover ? 140 : dialogCase == null ? 40 : 90));
                        results.Add(new(name, "passed", watch.ElapsedMilliseconds));
                        Console.WriteLine($"PASS {name}");
                    }
                    catch (Exception ex) {
                        failed = true;
                        results.Add(new(name, "failed", watch.ElapsedMilliseconds, ex.ToString()));
                        Console.Error.WriteLine($"FAIL {name}: {ex}");
                        try {
                            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                            bitmap.Render(window);
                            bitmap.Save(Path.Combine(output, $"failure-{name}.png"), PngBitmapEncoderOptions.Default);
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
