# 跟随系统主题

设置 → 外观提供三态：**跟随系统** / **浅色** / **深色**。新安装默认跟随系统；旧配置只有 `IsDarkTheme` 时仍映射为强制浅/深，外观不变。

## 行为

| 选择 | 运行时 | 侧栏主题按钮 |
|---|---|---|
| 跟随系统 | `ThemeVariant.Default`，OS 浅/深色变化时 UI、编辑器高亮、预览 HTML 同步 | 点一次退出跟随，切到当前实际主题的反色 |
| 浅色 | 强制浅色，不受 OS 影响 | 切到深色 |
| 深色 | 强制深色，不受 OS 影响 | 切到浅色 |

回到「跟随系统」只在设置里选。设置页改三态即时预览，保存后保留，撤销恢复上次保存值。跟随系统且草稿也是跟随系统时，OS 主题变化会更新实际浅/深，但不会把设置标脏。

兼容字段 `IsDarkTheme` 仅表示「强制深色」：`ThemeMode == Dark` 时为 true，其余为 false。真正 UI 以 `ThemeMode` 为准。

## 平台冒烟

不写平台分支代码；Avalonia `IPlatformSettings` 负责探测。各平台打一次即可。

| 平台 | 机制 | 验证 |
|---|---|---|
| Windows 10/11 | 系统应用浅/深色；标题栏随 `RequestedThemeVariant`（Win11 更完整） | 跟随 ↔ OS 切换实时；强制模式不受 OS 影响 |
| macOS | Appearance | 同上；`osx-x64` / `osx-arm64` 打包各冒烟一次 |
| Linux | FreeDesktop `color-scheme` 等 | GNOME/KDE 支持较好；部分 WM 可能一直报 Light——属平台限制，强制浅/深仍可用 |

HighContrast（Windows）：跟随系统时 FluentAvalonia 可能切入高对比。本轮不单独做第四态 UI。

## 手工清单

1. 跟随系统 → 改 OS 主题 → 壳层、预览 HTML、编辑器高亮同步。
2. 强制浅或深 → 改 OS → 应用不变。
3. 跟随下点侧栏 → 变为强制反色；设置里对应单选已保存/草稿正确。
4. 设置改三态即时预览 → 撤销回到保存值。
