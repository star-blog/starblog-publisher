using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Views;
using StarBlogPublisher.Views.Controls;

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
                ["background"] = window.Background?.ToString() ?? "",
                ["sidebarAlpha"] = BrushAlpha(TryWorkspaceSidebar()),
                ["editorAlpha"] = BrushAlpha(TryEditorCanvas()),
                ["inspectorAlpha"] = BrushAlpha(TryInspectorPane()),
                ["chromeAlpha"] = BrushAlpha(TryDocumentTabs())
            });
        }

        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Standard, "material-light-standard");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Standard, "material-dark-standard");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Mica, "material-light-mica");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Mica, "material-dark-mica");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Acrylic, "material-light-acrylic");
        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Acrylic, "material-dark-acrylic");
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Standard, "material-light-standard-restored");

        var article = Path.Combine(output, "material-workspace.md");
        await File.WriteAllTextAsync(article, "# 材质分层\n\n正文应保持不透明画布，侧栏和标签使用语义叠色。\n");
        shell.ActivePage = shell.Workspace;
        await shell.Workspace.OpenPathAsync(article);
        shell.Workspace.IsSidebarOpen = true;
        shell.Workspace.IsTaskPanelOpen = true;
        shell.PublishPage.IsInspectorOpen = true;
        await Task.Delay(300);

        var sidebarWidth = shell.Workspace.SidebarColumnWidth.Value;
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Mica, "workspace-light-mica");
        AssertWorkspaceLayering(InterfaceStyle.Mica);
        if (Math.Abs(shell.Workspace.SidebarColumnWidth.Value - sidebarWidth) > 0.5) {
            throw new Exception("Material preview changed sidebar width");
        }

        await CaptureStyle(ThemeMode.Dark, InterfaceStyle.Mica, "workspace-dark-mica");
        AssertWorkspaceLayering(InterfaceStyle.Mica);
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Acrylic, "workspace-light-acrylic");
        AssertWorkspaceLayering(InterfaceStyle.Acrylic);
        await CaptureStyle(ThemeMode.Light, InterfaceStyle.Standard, "workspace-light-standard");
        AssertWorkspaceLayering(InterfaceStyle.Standard);

        var restored = records.First(item => item["name"] == "material-light-standard-restored");
        if (restored["style"] != nameof(InterfaceStyle.Standard)) {
            throw new Exception("Failed to restore standard style after material cycling");
        }

        await File.WriteAllTextAsync(
            Path.Combine(output, "material.json"),
            JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void AssertWorkspaceLayering(InterfaceStyle style) {
        var editorAlpha = ParseAlpha(BrushAlpha(TryEditorCanvas()));
        if (editorAlpha != 255) {
            throw new Exception($"Editor canvas is not opaque under {style}: alpha={editorAlpha}");
        }

        var sidebarAlpha = ParseAlpha(BrushAlpha(TryWorkspaceSidebar()));
        var frosted = style != InterfaceStyle.Standard
            && WindowMaterialPolicy.IsFrostedMaterial(window.ActualTransparencyLevel);
        if (frosted && sidebarAlpha == 255) {
            throw new Exception($"Sidebar stayed fully opaque under {style}");
        }

        if (style == InterfaceStyle.Standard && sidebarAlpha != 255) {
            throw new Exception($"Standard sidebar is not opaque: alpha={sidebarAlpha}");
        }

        if (!shell.Workspace.IsSidebarOpen || !shell.Workspace.IsTaskPanelOpen || !shell.PublishPage.IsInspectorOpen) {
            throw new Exception("Material preview collapsed workspace panels");
        }
    }

    private Border? TryWorkspaceSidebar() =>
        window.GetVisualDescendants().OfType<ArticleWorkspaceView>().FirstOrDefault()?.FindControl<Border>("WorkspaceSidebar");

    private MarkdownEditorView? TryEditorCanvas() =>
        window.GetVisualDescendants().OfType<MarkdownEditorView>().FirstOrDefault();

    private Border? TryInspectorPane() =>
        window.GetVisualDescendants().OfType<PublishEditorView>().FirstOrDefault()?.FindControl<Border>("ArticleInspectorPane");

    private ListBox? TryDocumentTabs() =>
        window.GetVisualDescendants().OfType<ArticleWorkspaceView>().FirstOrDefault()?.FindControl<ListBox>("DocumentTabBar");

    private static string BrushAlpha(object? control) {
        var brush = control switch {
            Border border => border.Background,
            TemplatedControl templated => templated.Background,
            _ => null
        };
        return brush is ISolidColorBrush solid ? solid.Color.A.ToString() : "";
    }

    private static int ParseAlpha(string value) =>
        int.TryParse(value, out var alpha) ? alpha : -1;
}
