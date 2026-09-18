# Avalonia 12 升级记录：StarBlog Publisher

> 升级完成日期：2026-09-18。本文是后续撰写升级文章、排查兼容性问题时的项目内参考，不替代 [Avalonia 12 官方破坏性变更说明](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)。

## 结论

应用层的 XAML、MVVM、数据绑定和主题使用方式与 Avalonia 11 基本连续；实际迁移工作主要集中在窗口装饰、FluentAvalonia 3 的控件改名，以及拖放、剪贴板等平台 API。

本次升级同时将 Markdown 预览从 `Markdown.Avalonia` 替换为 Markdig 生成 HTML、再由原生 WebView 显示。预览排版能力因此不再受 Avalonia 控件渲染器限制。

## 已采用版本

| 组件 | 版本 | 说明 |
|---|---:|---|
| Avalonia | 12.1.2 | GUI 框架 |
| FluentAvaloniaUI | 3.1.0 | Fluent 应用壳与控件 |
| Avalonia.Controls.WebView | 12.1.0 | 跨平台原生 WebView 包 |
| Avalonia.AvaloniaEdit | 12.0.0 | Markdown 源码编辑器 |
| .NET | 10.0 | 项目目标框架 |

## 本项目的迁移清单

### 依赖与初始化

- 所有 Avalonia 核心包升级到 12.1.2；移除 Avalonia 12 中不再提供的 `Avalonia.Diagnostics`。
- `App.axaml.cs` 移除 `BindingPlugins.DataValidators` 的手动处理。Avalonia 12 不再使用旧版全局绑定插件入口。
- 删除 `Markdown.Avalonia`，加入 `Avalonia.Controls.WebView`。
- 所有旧的 `TextBox.Watermark` 改为 `PlaceholderText`，以消除 Avalonia 12 的弃用警告。

### FluentAvalonia 3 控件改名

FluentAvalonia 3 使用 `FA` 前缀区分新版控件。迁移时应同时检查 XAML、代码后置文件和对话框宿主。

| 旧名称 | 新名称 |
|---|---|
| `AppWindow` | `FAAppWindow` |
| `NavigationView` / `NavigationViewItem` | `FANavigationView` / `FANavigationViewItem` |
| `InfoBar` | `FAInfoBar` |
| `BreadcrumbBar` | `FABreadcrumbBar` |
| `SettingsExpander` / `SettingsExpanderItem` | `FASettingsExpander` / `FASettingsExpanderItem` |
| `NumberBox` | `FANumberBox` |
| `ContentDialog` / `TaskDialog` | `FAContentDialog` / `FATaskDialog` |

### 平台 API 适配

- 拖放：`DragEventArgs.Data` 和 `DataFormats.Files` 改为 `DragEventArgs.DataTransfer`、`DataFormat.File` 与 `TryGetFiles()`。
- 剪贴板：引入 `Avalonia.Input.Platform`，使用 `SetTextAsync()` 扩展方法。
- 窗口标题栏：Avalonia 12 重做窗口装饰体系，旧的 `TitleBarHitTestType` 已移除。FluentAvalonia 的标题栏仍可扩展内容，但当导航控件覆盖标题区域时，需要为实际的标题栏元素显式处理拖动。

本项目在 `MainWindow` 顶部透明标题区处理左键 `PointerPressed` 并调用 `BeginMoveDrag(e)`。这样保留系统最小化、最大化、关闭按钮，同时恢复标题文字和空白区域的窗口拖动。关于 Avalonia 12 的标准标题栏拖动区域与 `WindowDecorationProperties.ElementRole`，见 [官方窗口文档](https://docs.avaloniaui.net/docs/how-to/window-how-to)。

### Markdown 原生 WebView 预览

预览链路为：

```text
ArticleContent → Markdig（高级扩展、禁用原始 HTML）→ 临时 index.html → NativeWebView
```

- 生成的 HTML 内嵌阅读样式，覆盖标题、代码块、表格、引用、图片和深色模式。
- 打开 Markdown 文件后会写入 `<base href>`，因此相对图片路径仍按源文件所在目录解析。
- HTML 保存在系统临时目录 `StarBlogPublisher/markdown-preview/index.html`；追加时间戳查询参数以强制 WebView 刷新。
- Markdig 使用 `DisableHtml()`，避免把编辑内容中的原始 HTML 直接注入预览页。

相关实现：

- `StarBlogPublisher/Views/Controls/MarkdownEditorView.axaml`
- `StarBlogPublisher/ViewModels/PublishViewModel.cs`

`NativeWebView` 使用操作系统原生引擎：Windows 使用 WebView2、macOS 使用 WKWebView、Linux 使用 WebKitGTK/WPE。浏览器/WASM 目标目前不适合该控件；Linux 打包时还应验证目标系统的 WebKit 运行时依赖。详情见 [NativeWebView 文档](https://docs.avaloniaui.net/controls/web/nativewebview)。

## 验证清单

升级或改动相关代码后，至少执行：

```powershell
dotnet build StarBlogPublisher.sln --no-restore -v:minimal
dotnet test StarBlogPublisher.sln --no-build --no-restore -v:minimal
```

需要人工验收的项目：

1. 在标题文本和顶部空白区域拖动窗口；确认系统窗口按钮仍可用。
2. 打开包含代码块、表格、引用和相对图片的 Markdown，检查分栏与独立预览模式。
3. 在 Windows、macOS、Linux 的实际发布目标上分别检查原生 WebView 运行时。

## 写文章时可采用的叙述

推荐把这次升级描述为“应用层低摩擦、平台边缘 API 需要显式迁移”：业务和 MVVM 几乎不动，主要成本来自窗口装饰重构、第三方 UI 库的命名变更，以及为了获得高保真 Markdown 预览而选择原生 WebView。

对应提交：

- `aac9996 feat(gui): upgrade Avalonia 12 and native Markdown preview`
- `9fa1ede fix(gui): restore title bar dragging`
