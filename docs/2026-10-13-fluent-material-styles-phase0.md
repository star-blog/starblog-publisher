# Mica / Acrylic 阶段 0 验证记录

- 日期：2026-10-13
- 环境：Windows 11 专业版 25H2，OS build 26200，.NET 10，Avalonia 12.1.2，FluentAvaloniaUI 3.1.0
- 命令：`dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --material`
- 原始产物：`output/desktop-tests/20261013-032243-f111bc475d1346dfbc58294af481b510/`
- 对照截图：`docs/images/material-phase0/`

## 1. API 与遮挡

- `FAAppWindow` 继续作为主窗口。Windows 模板把 `Background` 绑到 `RootBorder`，没有 `PART_TransparencyFallback`。不能只依赖 `TransparencyBackgroundFallback`；不支持材质时必须显式绘制不透明底色。
- Avalonia `WindowTransparencyLevel.None` 在未绘制区域为黑色，`Transparent` 为无模糊裸透明。二者都不视为磨砂成功。
- `TitleBarBackground` 已是透明。`NavigationViewDefaultPaneBackground` 为不透明 `SolidBackgroundFillColorBaseBrush`，会挡住材质，阶段 2 覆盖为 `SidebarBackgroundBrush`。`NavigationViewContentBackground` 为半透明 `LayerFillColorDefaultBrush`。
- 高对比度：`FluentAvaloniaTheme.HighContrastTheme` 与 `IPlatformSettings.GetColorValues().ContrastPreference`。系统关闭透明效果时以 `ActualTransparencyLevel` 为准，不按操作系统名称宣布成功。

## 2. 本机实测

| 组合 | 请求 hint | 实际值 | 窗口背景 | 呈现 |
| --- | --- | --- | --- | --- |
| Light × 标准 | None | None | `#fff3f3f3` | 标准实色 |
| Dark × 标准 | None | None | `#ff202020` | 标准实色 |
| Light × Mica | Mica,AcrylicBlur,Blur | Mica | `#ccf3f3f3` | 原生 Mica |
| Dark × Mica | Mica,AcrylicBlur,Blur | Mica | `#cc202020` | 原生 Mica |
| Light × Acrylic | AcrylicBlur,Blur | AcrylicBlur | `#99f3f3f3` | 原生 Acrylic |
| Dark × Acrylic | AcrylicBlur,Blur | AcrylicBlur | `#99202020` | 原生 Acrylic |
| Light × 标准（切回） | None | None | `#fff3f3f3` | 标准实色，无残留请求 |

深色回退底为 `#202020`，未见白底。菜单、拖动区、标题栏按钮和命令搜索在对照截图中保持可用。Avalonia `RenderTargetBitmap` 不会画出 DWM 壁纸取样，原生 Mica／Acrylic 观感仍需在真实桌面肉眼确认。

## 3. 降级路径

- 标准：关闭材质请求，恢复主题背景。
- Mica：Mica → AcrylicBlur → Blur；得到 Acrylic／Blur 时提示兼容模糊。
- Acrylic：AcrylicBlur → Blur，不反向降为 Mica。
- 高对比度或实际结果为 None／Transparent：不透明 `MaterialFallbackBrush`，偏好字段不改写。

未在本机验证：Windows 10、macOS、Linux、远程桌面、软件渲染。这些环境保持上述安全回退，阶段 5 再记录。
