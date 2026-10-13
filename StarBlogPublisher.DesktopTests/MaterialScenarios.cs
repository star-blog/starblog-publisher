using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentAvalonia.Styling;
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
            var menu = window.FindControl<Menu>("MainMenu")!;
            var group = (MenuItem)menu.Items[2]!;
            group.IsSubMenuOpen = true;
            try {
                var root = await WaitForMenuRoot(group);
                await Task.Delay(100);
                AssertMenuMaterial(root, style);
                records[^1]["menuHint"] = string.Join(",", root.TransparencyLevelHint);
                records[^1]["menuActual"] = root.ActualTransparencyLevel.ToString();
                records[^1]["menuBackground"] = MenuSurface(root).Background?.ToString() ?? "";
                var acrylic = MenuAcrylicSurface(root);
                records[^1]["menuAcrylicLayer"] = acrylic.IsVisible.ToString();
                records[^1]["menuTintOpacity"] = acrylic.IsVisible ? acrylic.Material?.TintOpacity.ToString() ?? "" : "";
                records[^1]["menuMaterialOpacity"] = acrylic.IsVisible ? acrylic.Material?.MaterialOpacity.ToString() ?? "" : "";
                CaptureMenu(root, name + "-menu");
            }
            finally { group.IsSubMenuOpen = false; }
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

        await VerifyOpenMenuChanges();
        await VerifyNestedMenus();

        var restored = records.First(item => item["name"] == "material-light-standard-restored");
        if (restored["style"] != nameof(InterfaceStyle.Standard)) {
            throw new Exception("Failed to restore standard style after material cycling");
        }

        await File.WriteAllTextAsync(
            Path.Combine(output, "material.json"),
            JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task VerifyOpenMenuChanges() {
        var group = (MenuItem)window.FindControl<Menu>("MainMenu")!.Items[2]!;
        group.IsSubMenuOpen = true;
        try {
            var root = await WaitForMenuRoot(group);
            foreach (var style in new[] { InterfaceStyle.Mica, InterfaceStyle.Acrylic, InterfaceStyle.Standard }) {
                foreach (var theme in new[] { ThemeMode.Dark, ThemeMode.Light }) {
                    shell.PreviewTheme(theme);
                    shell.PreviewInterfaceStyle(style);
                    await Task.Delay(100);
                    AssertMenuMaterial(root, style);
                }
            }

            shell.PreviewInterfaceStyle(InterfaceStyle.Mica);
            window.RequestedThemeVariant = FluentAvaloniaTheme.HighContrastTheme;
            await Task.Delay(100);
            AssertMenuMaterial(root, InterfaceStyle.Mica, forceSolid: true);
        }
        finally {
            group.IsSubMenuOpen = false;
            window.ClearValue(ThemeVariantScope.RequestedThemeVariantProperty);
            shell.PreviewTheme(ThemeMode.Light);
            shell.PreviewInterfaceStyle(InterfaceStyle.Standard);
        }
    }

    private async Task VerifyNestedMenus() {
        var originalWidth = window.Width;
        try {
            window.Width = 800;
            shell.PreviewInterfaceStyle(InterfaceStyle.Acrylic);
            await Task.Delay(150);
            var compact = (MenuItem)window.FindControl<Menu>("CompactMenu")!.Items[0]!;
            compact.IsSubMenuOpen = true;
            try {
                AssertMenuMaterial(await WaitForMenuRoot(compact), InterfaceStyle.Acrylic);
                var file = (MenuItem)compact.Items[0]!;
                file.IsSubMenuOpen = true;
                AssertMenuMaterial(await WaitForMenuRoot(file), InterfaceStyle.Acrylic);
                var recent = (MenuItem)file.Items[3]!;
                if (!recent.IsEnabled) throw new Exception("Recent files submenu was not populated");
                recent.IsSubMenuOpen = true;
                var recentRoot = await WaitForMenuRoot(recent);
                AssertMenuMaterial(recentRoot, InterfaceStyle.Acrylic);
                shell.PreviewInterfaceStyle(InterfaceStyle.Standard);
                await Task.Delay(100);
                AssertMenuMaterial(recentRoot, InterfaceStyle.Standard);
            }
            finally { compact.IsSubMenuOpen = false; }
        }
        finally {
            window.Width = originalWidth;
            shell.PreviewInterfaceStyle(InterfaceStyle.Standard);
        }
    }

    private static async Task<PopupRoot> WaitForMenuRoot(MenuItem item) =>
        (await WaitUntilAsync(() => {
            var popup = item.GetTemplateDescendants().OfType<Popup>().FirstOrDefault(p => p.Name == "PART_Popup");
            return Task.FromResult(popup?.Child == null ? null : TopLevel.GetTopLevel(popup.Child) as PopupRoot);
        }, "Menu popup root did not open"))!;

    private static Border MenuSurface(PopupRoot root) =>
        ((Panel)root.Content!).Children.OfType<Border>().Single();

    private static ExperimentalAcrylicBorder MenuAcrylicSurface(PopupRoot root) =>
        ((Panel)root.Content!).Children.OfType<ExperimentalAcrylicBorder>().Single();

    private static void AssertMenuMaterial(PopupRoot root, InterfaceStyle style, bool forceSolid = false) {
        var hints = WindowMaterialPolicy.GetMenuTransparencyHints(style, forceSolid);
        if (!root.TransparencyLevelHint.SequenceEqual(hints)) {
            throw new Exception($"Menu did not follow {style}: hints={string.Join(",", root.TransparencyLevelHint)}");
        }
        var expectedMaterial = WindowMaterialPolicy.ShouldUseTransparentMenuBackground(style, forceSolid, root.ActualTransparencyLevel);
        var surface = MenuSurface(root);
        var alpha = ((ISolidColorBrush)surface.Background!).Color.A;
        var acrylic = MenuAcrylicSurface(root);
        var useAcrylic = expectedMaterial && style == InterfaceStyle.Acrylic;
        if (acrylic.IsVisible != useAcrylic || (useAcrylic && (alpha != 0 || acrylic.Material == null
            || acrylic.Material.BackgroundSource != AcrylicBackgroundSource.None))) {
            throw new Exception($"Acrylic menu did not use the material layer under {style}");
        }
        if ((expectedMaterial && !useAcrylic && alpha == 255) || (!expectedMaterial && alpha != 255)) {
            throw new Exception($"Menu surface did not match {style}: actual={root.ActualTransparencyLevel}, background={surface.Background}");
        }
    }

    private void CaptureMenu(PopupRoot root, string name) {
        using var bitmap = new RenderTargetBitmap(new Avalonia.PixelSize(
            (int)Math.Ceiling(root.Bounds.Width), (int)Math.Ceiling(root.Bounds.Height)));
        bitmap.Render(root);
        bitmap.Save(Path.Combine(output, name + ".png"), PngBitmapEncoderOptions.Default);
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
