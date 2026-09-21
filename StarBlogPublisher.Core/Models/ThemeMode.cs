namespace StarBlogPublisher.Models;

/// <summary>
/// 应用外观偏好。跟随系统时由 Avalonia 根据 OS 浅/深色更新实际主题。
/// </summary>
public enum ThemeMode {
    System = 0,
    Light = 1,
    Dark = 2
}

public static class ThemeModeHelper {
    /// <summary>
    /// 新字段优先；旧配置只有 <c>IsDarkTheme</c> 时映射为强制浅/深，避免老用户突然变成跟随系统。
    /// </summary>
    public static ThemeMode FromStorage(ThemeMode? themeMode, bool isDarkTheme) =>
        themeMode ?? (isDarkTheme ? ThemeMode.Dark : ThemeMode.Light);

    /// <summary>
    /// 兼容旧布尔语义：仅「强制深色」为 true，跟随系统与浅色均为 false。
    /// </summary>
    public static bool ToLegacyIsDarkTheme(ThemeMode themeMode) => themeMode == ThemeMode.Dark;
}
