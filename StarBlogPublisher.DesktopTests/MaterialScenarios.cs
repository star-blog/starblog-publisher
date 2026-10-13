using System.Text.Json;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.DesktopTests;

internal sealed partial class WorkspaceScenarios {
    public async Task MaterialShell() {
        shell.ActivePage = shell.SettingsPage;
        await Task.Delay(200);
        var records = new List<Dictionary<string, string>>();

        async Task CaptureStyle(ThemeMode theme, InterfaceStyle style, string name) {
            shell.PreviewTheme(theme);
            shell.PreviewInterfaceStyle(style);
            await Task.Delay(400);
            Capture(window, Path.Combine(output, name + ".png"));
            records.Add(new Dictionary<string, string> {
                ["name"] = name,
                ["theme"] = theme.ToString(),
                ["style"] = style.ToString(),
                ["hint"] = string.Join(",", window.TransparencyLevelHint),
                ["actual"] = window.ActualTransparencyLevel.ToString(),
                ["presentation"] = shell.MaterialStatus.Presentation.ToString(),
                ["description"] = shell.MaterialStatus.Description,
                ["isWindows11"] = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000).ToString(),
                ["os"] = Environment.OSVersion.VersionString,
                ["background"] = window.Background?.ToString() ?? ""
            });
        }

        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Standard, "material-light-standard");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Standard, "material-dark-standard");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Mica, "material-light-mica");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Mica, "material-dark-mica");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Acrylic, "material-light-acrylic");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Acrylic, "material-dark-acrylic");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Standard, "material-light-standard-restored");

        var restored = records[^1];
        if (restored["style"] != nameof(InterfaceStyle.Standard)) {
            throw new Exception("Failed to restore standard style after material cycling");
        }

        await File.WriteAllTextAsync(
            Path.Combine(output, "material.json"),
            JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    }
}
