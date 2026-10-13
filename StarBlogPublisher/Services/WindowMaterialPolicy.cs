using System.Collections.Generic;
using Avalonia.Controls;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

public enum WindowMaterialPresentation {
    StandardSolid,
    NativeMica,
    NativeAcrylic,
    CompatibilityBlur,
    SolidFallback
}

public readonly record struct WindowMaterialStatus(
    InterfaceStyle RequestedStyle,
    WindowTransparencyLevel ActualLevel,
    WindowMaterialPresentation Presentation,
    string Description);

/// <summary>
/// 候选材质顺序与回退判断。不依赖窗口句柄，便于单元测试。
/// </summary>
public static class WindowMaterialPolicy {
    // Popup roots need transparent corners for the existing rounded menu template.
    // Transparent is only a host fallback; the menu surface stays opaque unless
    // the platform actually supplies the material selected by the user.
    public static IReadOnlyList<WindowTransparencyLevel> GetMenuTransparencyHints(InterfaceStyle style, bool forceSolid) =>
        forceSolid ? [WindowTransparencyLevel.Transparent] : style switch {
            InterfaceStyle.Mica => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.Transparent],
            InterfaceStyle.Acrylic => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent],
            _ => [WindowTransparencyLevel.Transparent]
        };

    public static bool ShouldUseTransparentMenuBackground(
        InterfaceStyle style, bool forceSolid, WindowTransparencyLevel actual) =>
        !forceSolid && (style == InterfaceStyle.Mica && actual == WindowTransparencyLevel.Mica
            || style == InterfaceStyle.Acrylic && actual == WindowTransparencyLevel.AcrylicBlur);

    public static IReadOnlyList<WindowTransparencyLevel> GetTransparencyHints(InterfaceStyle style, bool forceSolid) {
        if (forceSolid || style == InterfaceStyle.Standard) {
            return [WindowTransparencyLevel.None];
        }

        return style == InterfaceStyle.Mica
            ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur]
            : [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur];
    }

    public static bool IsFrostedMaterial(WindowTransparencyLevel actual) =>
        actual == WindowTransparencyLevel.Mica
        || actual == WindowTransparencyLevel.AcrylicBlur
        || actual == WindowTransparencyLevel.Blur;

    public static bool ShouldUseTransparentWindowBackground(
        InterfaceStyle style, bool forceSolid, WindowTransparencyLevel actual) =>
        !forceSolid && style != InterfaceStyle.Standard && IsFrostedMaterial(actual);

    public static WindowMaterialPresentation ResolvePresentation(
        InterfaceStyle requested, bool forceSolid, WindowTransparencyLevel actual) {
        if (requested == InterfaceStyle.Standard) {
            return WindowMaterialPresentation.StandardSolid;
        }

        if (forceSolid || !IsFrostedMaterial(actual)) {
            return WindowMaterialPresentation.SolidFallback;
        }

        if (actual == WindowTransparencyLevel.Mica) {
            return WindowMaterialPresentation.NativeMica;
        }

        if (actual == WindowTransparencyLevel.AcrylicBlur && requested == InterfaceStyle.Acrylic) {
            return WindowMaterialPresentation.NativeAcrylic;
        }

        return WindowMaterialPresentation.CompatibilityBlur;
    }

    public static string GetStatusDescription(WindowMaterialPresentation presentation) => presentation switch {
        WindowMaterialPresentation.NativeMica => "当前使用原生 Mica",
        WindowMaterialPresentation.NativeAcrylic => "当前使用原生 Acrylic",
        WindowMaterialPresentation.CompatibilityBlur => "当前使用兼容模糊效果",
        WindowMaterialPresentation.SolidFallback => "当前环境不支持，已使用实色背景",
        _ => "当前使用标准实色界面"
    };

    public static WindowMaterialStatus CreateStatus(
        InterfaceStyle requested, bool forceSolid, WindowTransparencyLevel actual) {
        var presentation = ResolvePresentation(requested, forceSolid, actual);
        return new WindowMaterialStatus(requested, actual, presentation, GetStatusDescription(presentation));
    }
}
