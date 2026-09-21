using System.Linq;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

/// <summary>
/// 将 <see cref="ThemeMode"/> 应用到 Avalonia / FluentAvalonia。持久化由壳层负责。
/// </summary>
public static class AppThemeService {
    public static void Apply(ThemeMode mode) {
        var app = Avalonia.Application.Current;
        if (app == null) return;

        var followSystem = mode == ThemeMode.System;
        var fluentTheme = app.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
        if (fluentTheme != null) {
            // FA may set RequestedThemeVariant to concrete Light/Dark while following the OS; use PreferSystemTheme + ActualThemeVariant.
            fluentTheme.PreferSystemTheme = followSystem;
        }

        app.RequestedThemeVariant = mode switch {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    public static bool ResolveEffectiveIsDark(ThemeMode mode) {
        if (mode == ThemeMode.Light) return false;
        if (mode == ThemeMode.Dark) return true;
        return Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }
}
