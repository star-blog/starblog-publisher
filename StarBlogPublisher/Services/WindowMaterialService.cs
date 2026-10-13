using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

/// <summary>
/// 将界面风格应用到主窗口及菜单弹出窗口：请求材质、监听实际结果、叠色与实色回退。
/// </summary>
public sealed class WindowMaterialService : IDisposable {
    private static readonly string[] OverlayKeys = [
        "ShellBackgroundBrush",
        "SidebarBackgroundBrush",
        "WorkspaceCanvasBrush",
        "InspectorBackgroundBrush",
        "WorkspaceChromeBrush",
        "MaterialFallbackBrush",
        "NavigationViewDefaultPaneBackground",
        "NavigationViewExpandedPaneBackground",
        "NavigationViewContentBackground"
    ];

    private Window? _window;
    private readonly Dictionary<PopupRoot, ExperimentalAcrylicBorder> _menuRoots = [];
    private InterfaceStyle _requested = InterfaceStyle.Standard;
    private bool _applying;
    private bool _disposed;

    public WindowMaterialStatus Status { get; private set; } =
        WindowMaterialPolicy.CreateStatus(InterfaceStyle.Standard, false, WindowTransparencyLevel.None);

    public event EventHandler? StatusChanged;

    public void Attach(Window window) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_window, window)) return;
        Detach();
        _window = window;
        _window.PropertyChanged += OnWindowPropertyChanged;
        _window.Closed += OnWindowClosed;
        var platformSettings = GetPlatformSettings();
        if (platformSettings != null) {
            platformSettings.ColorValuesChanged += OnColorValuesChanged;
        }
    }

    public void Apply(InterfaceStyle style, bool isDark) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _requested = InterfaceStyleHelper.FromStorage(style);
        _ = isDark;
        ApplyCore();
    }

    internal void AttachMenuPopup(Popup popup, ExperimentalAcrylicBorder acrylic) {
        if (_disposed || _window == null
            || popup.Child == null || TopLevel.GetTopLevel(popup.Child) is not PopupRoot root
            || !_menuRoots.TryAdd(root, acrylic)) return;

        root.PropertyChanged += OnMenuRootPropertyChanged;
        root.Closed += OnMenuRootClosed;
        ApplyMenuRoot(root);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }

    private void Detach() {
        if (_window == null) return;
        foreach (var (root, acrylic) in _menuRoots) {
            root.PropertyChanged -= OnMenuRootPropertyChanged;
            root.Closed -= OnMenuRootClosed;
            root.Resources.Remove("MenuFlyoutPresenterBackground");
            root.TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            root.ClearValue(TopLevel.BackgroundProperty);
            root.ClearValue(TopLevel.TransparencyBackgroundFallbackProperty);
            acrylic.IsVisible = false;
        }
        _menuRoots.Clear();
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _window.Closed -= OnWindowClosed;
        var platformSettings = GetPlatformSettings();
        if (platformSettings != null) {
            platformSettings.ColorValuesChanged -= OnColorValuesChanged;
        }
        RestoreWindow();
        _window = null;
    }

    private void OnWindowClosed(object? sender, EventArgs e) => Dispose();

    private void OnMenuRootClosed(object? sender, EventArgs e) {
        if (sender is not PopupRoot root) return;
        root.PropertyChanged -= OnMenuRootPropertyChanged;
        root.Closed -= OnMenuRootClosed;
        _menuRoots.Remove(root);
    }

    private void OnMenuRootPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (!_applying && sender is PopupRoot root
            && (e.Property == TopLevel.ActualTransparencyLevelProperty
                || e.Property == TopLevel.ActualThemeVariantProperty)) {
            ApplyMenuRoot(root);
        }
    }

    private void OnColorValuesChanged(object? sender, PlatformColorValues e) => ApplyCore();

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == TopLevel.ActualTransparencyLevelProperty
            || e.Property == TopLevel.ActualThemeVariantProperty) {
            ApplyCore();
        }
    }

    private void ApplyCore() {
        if (_applying || _window == null) return;
        _applying = true;
        try {
            var forceSolid = ShouldForceSolid();
            var hints = WindowMaterialPolicy.GetTransparencyHints(_requested, forceSolid);
            if (!HintsEqual(_window.TransparencyLevelHint, hints)) {
                _window.TransparencyLevelHint = hints;
            }

            var actual = _window.ActualTransparencyLevel;
            var frosted = WindowMaterialPolicy.ShouldUseTransparentWindowBackground(_requested, forceSolid, actual);
            ApplyResourceOverlays(frosted ? _requested : InterfaceStyle.Standard, frosted);
            ApplyWindowBackground(frosted, forceSolid);
            foreach (var root in _menuRoots.Keys) ApplyMenuRoot(root);

            var status = WindowMaterialPolicy.CreateStatus(_requested, forceSolid, actual);
            if (status != Status) {
                Status = status;
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally {
            _applying = false;
        }
    }

    private bool ShouldForceSolid() {
        if (_window == null) return false;
        if (_window.ActualThemeVariant == FluentAvaloniaTheme.HighContrastTheme) return true;
        return GetPlatformSettings()?.GetColorValues().ContrastPreference == ColorContrastPreference.High;
    }

    private void ApplyMenuRoot(PopupRoot root) {
        var wasApplying = _applying;
        _applying = true;
        try {
            var forceSolid = ShouldForceSolid() || root.ActualThemeVariant == FluentAvaloniaTheme.HighContrastTheme;
            var hints = WindowMaterialPolicy.GetMenuTransparencyHints(_requested, forceSolid);
            root.Background = Brushes.Transparent;
            if (!HintsEqual(root.TransparencyLevelHint, hints)) root.TransparencyLevelHint = hints;

            // Override only this popup's semantic brush; its template keeps the
            // existing rounding, border, shadow, text and interaction states.
            var useMaterial = WindowMaterialPolicy.ShouldUseTransparentMenuBackground(
                _requested, forceSolid, root.ActualTransparencyLevel);
            var acrylic = _menuRoots[root];
            acrylic.IsVisible = false;
            if (useMaterial && _requested == InterfaceStyle.Acrylic
                && root.TryFindResource("Material.Acrylic.MenuMaterial", root.ActualThemeVariant, out var materialValue)
                && materialValue is ExperimentalAcrylicMaterial material) {
                // Keep the per-popup material instance stable: the Acrylic control
                // subscribes to it and adjusts its platform compensation.
                var instance = acrylic.Material!;
                instance.BackgroundSource = material.BackgroundSource;
                instance.TintColor = material.TintColor;
                instance.TintOpacity = material.TintOpacity;
                instance.MaterialOpacity = material.MaterialOpacity;
                instance.FallbackColor = material.FallbackColor;
                acrylic.IsVisible = true;
                root.Resources["MenuFlyoutPresenterBackground"] = Brushes.Transparent;
            }
            else {
                var key = $"Material.{_requested}.MenuBackgroundBrush";
                if (useMaterial && root.TryFindResource(key, root.ActualThemeVariant, out var value) && value is IBrush brush) {
                    root.Resources["MenuFlyoutPresenterBackground"] = brush;
                }
                else {
                    root.Resources.Remove("MenuFlyoutPresenterBackground");
                }
            }

            if (TryFindBrush("MaterialFallbackBrush", out var fallback)) {
                root.TransparencyBackgroundFallback = fallback;
            }
        }
        finally {
            _applying = wasApplying;
        }
    }

    private IPlatformSettings? GetPlatformSettings() =>
        Avalonia.Application.Current?.PlatformSettings;

    private void ApplyResourceOverlays(InterfaceStyle effective, bool frosted) {
        if (_window == null) return;
        Assign("ShellBackgroundBrush", $"Material.{effective}.ShellBackgroundBrush");
        Assign("SidebarBackgroundBrush", $"Material.{effective}.SidebarBackgroundBrush");
        Assign("WorkspaceCanvasBrush", $"Material.{effective}.WorkspaceCanvasBrush");
        Assign("InspectorBackgroundBrush", $"Material.{effective}.InspectorBackgroundBrush");
        Assign("WorkspaceChromeBrush", $"Material.{effective}.WorkspaceChromeBrush");
        Assign("MaterialFallbackBrush", "MaterialFallbackBrush");

        if (!frosted || effective == InterfaceStyle.Standard) {
            _window.Resources.Remove("NavigationViewDefaultPaneBackground");
            _window.Resources.Remove("NavigationViewExpandedPaneBackground");
            _window.Resources.Remove("NavigationViewContentBackground");
            return;
        }

        if (TryFindBrush("SidebarBackgroundBrush", out var sidebar)) {
            _window.Resources["NavigationViewDefaultPaneBackground"] = sidebar;
            _window.Resources["NavigationViewExpandedPaneBackground"] = sidebar;
        }

        if (TryFindBrush("ShellBackgroundBrush", out var shell)) {
            _window.Resources["NavigationViewContentBackground"] = shell;
        }
    }

    private void ApplyWindowBackground(bool frosted, bool forceSolid) {
        if (_window == null) return;
        if (_requested == InterfaceStyle.Standard && !forceSolid) {
            _window.ClearValue(Window.BackgroundProperty);
            _window.ClearValue(TopLevel.TransparencyBackgroundFallbackProperty);
            return;
        }

        if (TryFindBrush("MaterialFallbackBrush", out var fallback)) {
            _window.TransparencyBackgroundFallback = fallback;
            if (!frosted) {
                _window.Background = fallback;
                return;
            }
        }

        if (frosted && TryFindBrush("ShellBackgroundBrush", out var shell)) {
            _window.Background = shell;
        }
    }

    private void RestoreWindow() {
        if (_window == null) return;
        _applying = true;
        try {
            _window.TransparencyLevelHint = [WindowTransparencyLevel.None];
            _window.ClearValue(Window.BackgroundProperty);
            _window.ClearValue(TopLevel.TransparencyBackgroundFallbackProperty);
            foreach (var key in OverlayKeys) {
                _window.Resources.Remove(key);
            }
        }
        finally {
            _applying = false;
        }
    }

    private void Assign(string liveKey, string sourceKey) {
        if (TryFindBrush(sourceKey, out var brush)) {
            _window!.Resources[liveKey] = brush;
        }
    }

    private bool TryFindBrush(string key, out IBrush brush) {
        brush = Brushes.Transparent;
        if (_window == null) return false;
        if (_window.TryFindResource(key, _window.ActualThemeVariant, out var value) && value is IBrush found) {
            brush = found;
            return true;
        }

        return false;
    }

    private static bool HintsEqual(
        IReadOnlyList<WindowTransparencyLevel> left,
        IReadOnlyList<WindowTransparencyLevel> right) {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++) {
            if (left[i] != right[i]) return false;
        }

        return true;
    }
}
