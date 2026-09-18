# GUI：FluentAvalonia 打磨基线

本文件是 FluentAvalonia GUI 的当前维护基线，补充并取代 `2026-09-18-gui-fluentavalonia-status.md` 中已经完成的 P0/P1 待办。旧 SukiUI 文档仅保留信息架构参考，不再作为实现依据。

## 锁定范围

| 项目 | 当前选择 |
|---|---|
| UI 工具包 | FluentAvaloniaUI 2.5.1 |
| UI 框架 | Avalonia 11.3.20 |
| 架构 | 单一 `AppWindow` + 四级 `NavigationView` |
| 业务层 | Core / CLI / MCP 不因 GUI 打磨改变规则 |

Avalonia 12、FluentAvalonia 3.x、AOT 兼容性回归与 GUI 自动化测试仍为独立后续议题。

## 已完成的体验基线

- 通用图标统一为 Fluent `SymbolIcon`；仅微信保留 FontAwesome Brands 标识。
- 壳使用 `PaneHeader`，并在页面顶部提供 Fluent `InfoBar` 反馈；警告和确认使用 `TaskDialog`。
- 内容型弹窗使用受限尺寸与滚动的 `ContentDialog`；分类弹窗与词云均采用明确的关闭和加载行为。
- 设置页使用 `SettingsExpander` 分组，数值字段使用 `NumberBox`，保存/还原操作固定在底栏。
- `BusyOverlay` 是页面和弹窗长任务的唯一遮罩组件；当前接入发布与词云。
- `StackPageHost` 与 `StackBreadcrumb` 提供根页面、二级页面与面包屑的可复用宿主；发布流程已接入。
- 全局提供 `Card`、`Compact`、`Icon` 与 `PageContent` 样式原语。新增页面应优先使用这些原语，而非复制留白和图标按钮尺寸。

## 后续准则

1. 新的设置项进入现有 `SettingsExpander`，或新增一个按业务域命名的分组；不要恢复为 Tab + Card 堆叠。
2. 长任务必须呈现 `BusyOverlay`，并提供清晰、面向用户的状态文案。
3. 可返回的二级页面通过 `StackPageHost` 接入，不另建顶级窗口或复制面包屑实现。
4. 新增通用操作图标使用 `SymbolIcon`；品牌图标是唯一允许使用 FontAwesome 的例外。
5. 视觉改动至少运行 `dotnet build StarBlogPublisher/StarBlogPublisher.csproj`；可交互变更还应进行启动冒烟检查。
