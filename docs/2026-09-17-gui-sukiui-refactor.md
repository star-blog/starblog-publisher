# Plan: GUI 重构为 SukiUI 壳 + 四级侧栏导航

## Goal

把桌面 GUI 从「FluentTheme + 单页主窗口 + 一堆独立弹窗」改成 Windows 现代化的单壳应用：

1. 左侧可折叠 drawer（`SukiSideMenu`）
2. 右侧主体页面
3. 其余界面全部换成 SukiUI 组件与主题

业务逻辑继续留在 `StarBlogPublisher.Core`。本次只动 `StarBlogPublisher/` 表现层。

## Non-Goals

1. 不改 Core / CLI / MCP 的业务规则
2. 不在本次引入新的博客功能
3. 不升级 Avalonia 12，不上 SukiUI 7
4. 不开启 AOT
5. 不把每一个现有窗口都做成侧栏菜单项

## Locked Decisions

### 1. 组件库版本

使用 **SukiUI 6.1.1**，Avalonia 保持 **11.3.20**。

| 选项 | 结论 |
|------|------|
| SukiUI 6.0.3 | 不用。Avalonia 11 线的收尾版是 6.1.1 |
| SukiUI 6.1.1 | **采用。** Avalonia ≥ 11.3.14，与当前 11.3.20 兼容 |
| SukiUI 7.0.1 | 不用。7 是同一套控件迁到 Avalonia 12，不是新 UI |

7.0.1 是 NuGet 稳定版，但 changelog 实质是 Avalonia 12 适配。升 7 等于同时升框架：`Markdown.Avalonia` 目前只有 12 预览包，剪贴板 API、`BindingPlugins`、`Avalonia.Diagnostics` 都会变。GUI 拆壳已经够大，框架升级单独做。

6 和 7 的壳 API 相同（`SukiWindow`、`SukiSideMenu`、`SukiStackPage`、`TabControl`、`GlassCard`、Dialog/Toast）。按 6 写的页面结构以后可以迁 7。

### 2. 侧栏范围：四级导航

侧栏只放一级去处，不把 11 个窗口平移成菜单：

```
SukiWindow
├── Hosts: SukiToastHost + SukiDialogHost
├── 标题栏右侧: 主题切换、登录状态
└── SukiSideMenu（可折叠，Compact）
    Header: Logo + StarBlog Publisher
    1. 发布
    2. 公众号排版
    3. 设置          ← 合并现有 AI 设置窗口
    4. 关于
    Footer: 登录状态 / 版本
```

发布页内部：

| 机制 | 用途 |
|------|------|
| `TabControl` | 编辑 / 预览 / 结果 |
| `SukiStackPage` | 图片分析、封面提示词（有返回路径） |

继续用 Dialog、不进侧栏：

- 添加分类
- 登录提示 / 确认 / 输入
- 分类词云

设置页用 `SettingsLayout`，分区：后端、代理、微信、图片、AI。

背景用 `BackgroundStyle="Flat"`，更接近 Windows 11；不采用 Gradient / Bubble 玻璃风作为默认。

## Current Findings

### 有利条件

1. GUI 与 Core 已经拆开，ViewModel 调 Application 服务，界面可以整层替换
2. 已有 `ViewLocator`（`XxxViewModel` → `XxxView`），适合侧栏页面导航
3. 主题、登录、发布、预览、公众号、设置等功能边界清楚，适合拆成页面

### 现状问题

1. 主窗口是单页塞满：顶栏 + 左右分栏 + 底栏按钮
2. 11 个 `Window` 全部 `ShowDialog`，没有应用壳
3. ViewModel 直接 `new XxxWindow()`，和视图绑死
4. `App.MainWindow` 静态引用到处用
5. 主题是 `FluentTheme` + 全局 `Button`/`TextBox`/`Border.Card` 样式
6. 刷子用 Fluent 资源（`SystemAccentColor`、`SystemChromeLowColor` 等）
7. 反馈靠底栏 `StatusMessage` + `MessageBox.Avalonia`
8. 自定义 `ConfirmDialog` / `InputDialog` / `DialogWindow` 与主题无关

### 现有窗口清单

| 窗口 | 重构去向 |
|------|----------|
| `MainWindow` | 应用壳 `SukiWindow`；内容拆成发布页 |
| 发布工作区（主窗口主体） | 侧栏「发布」+ Tab：编辑 / 预览 / 结果 |
| `PreviewWindow` | 发布页「预览」Tab |
| `PublishResultWindow` | 发布页「结果」Tab |
| `WeChatPublishWindow` | 侧栏「公众号排版」 |
| `SettingsWindow` | 侧栏「设置」 |
| `AiSettingsWindow` | 并入设置页 AI 分区 |
| `AboutWindow` | 侧栏「关于」 |
| `ImageGalleryWindow` | 发布页 `SukiStackPage` |
| `CoverPromptWindow` | 发布页 `SukiStackPage` |
| `AddCategoryWindow` | Dialog |
| `WordCloudWindow` | Dialog |

## Target Architecture

只改 GUI 宿主层：

```
StarBlogPublisher/
├── App.axaml                 SukiTheme，去掉 FluentTheme
├── Views/
│   ├── MainWindow            唯一 SukiWindow 壳
│   └── Pages/                发布 / 公众号 / 设置 / 关于
├── ViewModels/
│   ├── MainWindowViewModel   壳：导航、主题、Toast/Dialog Manager
│   └── 各页面 ViewModel
├── Controls/                 仅保留确有必要的自定义控件
└── Services/                 导航服务（可选，GUI 内）
```

原则：

1. 只有一个 `SukiWindow`
2. 侧栏选中项绑定页面 ViewModel，用现有 `ViewLocator` 出 View
3. ViewModel 不再 `new Window()`，改为 Navigate / Push / ShowDialog
4. Toast 与 Dialog 挂在壳的 `Hosts` 上，全应用共用
5. Core 项目零改动

`MainWindowViewModel` 过胖，按页面拆：`PublishViewModel`、`WeChatViewModel`、`SettingsViewModel`、`AboutViewModel`。壳 VM 只负责导航、主题、登录态、全局反馈。

`SukiSideMenuItem.PageContent` 不能空，否则会抛 visual parent 异常。MVVM 下用 `ItemsSource` + `ItemTemplate` + ViewLocator，不要在模板里塞空 `PageContent`。

## Component Mapping

| 现在 | 换成 |
|------|------|
| `Window` | 壳用 `SukiWindow`；页面用 `UserControl` |
| `FluentTheme` | `SukiTheme Locale="zh-CN" ThemeColor="Blue"` |
| `Border Classes="Card"` | `GlassCard` |
| 全局 `Button` 样式 | Suki `Basic` / `Flat` / `Accent`。**现有全局样式必须删除** |
| `SystemAccentColor` 等 Fluent 刷子 | `SukiPrimaryColor`、`SukiText`、`SukiCardBackground`、`SukiLowText` |
| `MessageBox.Avalonia` | `ISukiDialogManager` / `SukiMessageBox` |
| 底栏 `StatusMessage` | Toast（完成/失败）+ 发布中 `BusyArea` / ProgressBar |
| `LoadingIndicators.Avalonia` | Suki 进度控件 |
| `ConfirmDialog` / `InputDialog` / `DialogWindow` | `SukiDialog` |
| 顶栏设置 / 关于按钮 | 侧栏项 |
| 底栏一排动作 | 发布页工具栏 |

图标继续用 FontAwesome（Projektanker），不强制换成 Material Icons。

Markdown 预览继续用现有 `Markdown.Avalonia` 11.0.3，放进发布页预览 Tab。

## Package Changes

`StarBlogPublisher.csproj`：

- 新增：`SukiUI` 6.1.1
- 移除：`Avalonia.Themes.Fluent`
- 计划移除（第 3 期清依赖）：`MessageBox.Avalonia`、`LoadingIndicators.Avalonia`
- 保留：`Markdown.Avalonia`、`Projektanker.Icons.Avalonia.FontAwesome`、`CommunityToolkit.Mvvm`

注意：必须设置 `ThemeColor`，否则窗口和控件会透明。

## Phases

### Phase 0 — 基础设施

目标：应用能以 Suki 壳启动，现有功能暂不拆页。

1. 引用 SukiUI 6.1.1，去掉 `Avalonia.Themes.Fluent`
2. `App.axaml` 换成 `SukiTheme Locale="zh-CN" ThemeColor="Blue"`
3. 删除全局 Fluent 按钮 / 卡片样式
4. `MainWindow` 改为 `SukiWindow`，挂 Toast / Dialog Host
5. 放入可折叠 `SukiSideMenu`，右侧先仍是当前主界面内容
6. 验证亮暗主题、侧栏折叠、登录态显示

完成标准：能打开文件、编辑元数据、发布；外观已是 Suki，信息架构尚未切换。

### Phase 1 — 主壳 + 发布页

1. 侧栏四项可用，默认停在「发布」
2. 主窗口主体拆成 `PublishView` / `PublishViewModel`
3. 底栏动作收到发布页工具栏
4. 预览、发布结果改为发布页 Tab，不再弹窗
5. 壳 VM 只保留导航、主题、登录

完成标准：发布主路径（选文件 → 填信息 → 预览 → 发布 → 看结果）都在壳内完成。

### Phase 2 — 设置 / AI / 关于

1. 设置改为侧栏页 + `SettingsLayout`
2. AI 设置并入设置页，去掉独立入口
3. 关于改为侧栏页
4. 主题切换放到标题栏右侧，设置里可保留开关

完成标准：不再弹出设置 / AI / 关于窗口。

### Phase 3 — 其余功能 + 清依赖

1. 公众号排版改为侧栏页
2. 图片分析、封面提示词走 `SukiStackPage`
3. 添加分类、词云、确认/输入走 `SukiDialog`
4. 成功/失败用 Toast
5. 删除 `MessageBox.Avalonia`、自定义对话框、`LoadingIndicators.Avalonia`
6. 去掉 `App.MainWindow` 静态引用

完成标准：GUI 项目里不再 `new XxxWindow()`（壳除外）；不再引用 Fluent / MessageBox.Avalonia。

## Risks

| 风险 | 处理 |
|------|------|
| 全局 Button 样式盖住 Suki 主题 | Phase 0 删除，改用 Suki 按钮 class |
| `SukiSideMenuItem.PageContent` 为空抛异常 | 用 ViewLocator，保证每项有对应页面 |
| 6.1.1 依赖 SkiaSharp preview native assets | Linux 打包时检查；Windows 主路径优先验证 |
| `ViewLocator` 反射与未来 AOT | 本次不开 AOT；以后升 12 / 开 AOT 再评估 |
| 发布页状态（已加载文章、发布结果）在切走侧栏后丢失 | 页面 VM 由壳持有，不要每次选中都 new |
| Markdown 预览在 Tab 里的布局 | 沿用现有左右分栏，放入预览 Tab 再调 |

## Follow-up（不在本次）

Avalonia 12 + SukiUI 7 作为独立升级：

- 全套 Avalonia 包升到 12
- `Markdown.Avalonia` 换成 12 兼容包或替代渲染器
- 剪贴板、Diagnostics、数据校验插件按 Avalonia 12 breaking changes 改
- 核 `MessageBox` 替代品（届时应已换成 Suki Dialog）和 FontAwesome 兼容性

## Verification

每期至少：

1. `dotnet build StarBlogPublisher.sln`
2. `dotnet run --project StarBlogPublisher`
3. 走通：登录 → 打开 Markdown → 填分类 → 预览 → 发布（或确认发布按钮可用）
4. 切换亮/暗主题
5. 折叠/展开侧栏
6. Phase 2 起：设置保存后回到发布页，登录态和 AI 开关仍然正确

CLI / MCP 行为不应变化。若 GUI 改动碰到 Core，立刻停下来，那不属于本计划。
