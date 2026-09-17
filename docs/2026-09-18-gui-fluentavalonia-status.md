# GUI：FluentAvalonia 迁移现状

## 背景

2026-09-17 完成了以 SukiUI 为目标的 GUI 壳重构（见 `docs/2026-09-17-gui-sukiui-refactor.md`）。随后观感不理想，改为研究并落地 [FluentAvalonia](https://github.com/amwx/FluentAvalonia)（NuGet：`FluentAvaloniaUI`）。

最初按「最小可运行原型」推进：先换主题与壳，验证 Windows Fluent 观感。为了去掉双主题并存、让应用真正能跑，实现上变成了**整库替换**：卸载 SukiUI，主路径改到 FluentAvalonia / Avalonia 控件。

本文记录截至 2026-09-18 的真实完成度，以及尚未完成项。它不是实现计划的替代品；旧 Suki 文档保留作历史，**新工作以本文与后续 Fluent 计划为准**。

## 当前锁定

| 项 | 选择 |
|----|------|
| UI 工具包 | **FluentAvaloniaUI 2.5.1** |
| Avalonia | **11.3.20**（不在本轮升 12） |
| FluentAvalonia 3.x | 不用；依赖 Avalonia ≥ 12.1，与 `Markdown.Avalonia` 等升级一并评估 |
| 信息架构 | 保留单壳 + 四级侧栏（发布 / 公众号排版 / 设置 / 关于） |
| Core / CLI / MCP | 不改业务规则 |

## 已完成

### 依赖与主题

- 移除 `SukiUI`
- 引入 `FluentAvaloniaUI` 2.5.1
- `App.axaml` 使用 `FluentAvaloniaTheme`
- 提供全局 `Border.Card` 样式，替代原 `GlassCard` 用法

### 应用壳

- 主窗：`AppWindow`（扩展标题栏）
- 导航：`NavigationView` + `MenuItemsSource` 绑定页面 VM
- 主题切换、登录 / 注销移到侧栏 `PaneFooter`
- 壳 VM 不再依赖 Suki 的 Theme / Toast / Dialog Manager

### 反馈宿主（GuiHost）

- Toast：Avalonia `WindowNotificationManager`，位置右下角
- 确认 / 提示 / 输入：`ContentDialog`
- 内容型弹层（词云、添加分类）：`ShowContentAsync` + ViewLocator
- `AddCategoryViewModel` 通过 `IDialogHostAware` 关闭对话框（不再使用 `ISukiDialog`）

### 页面去 Suki

- `GlassCard` → `Border Classes="Card"`
- Suki 色刷 → Fluent 资源（如 `TextFillColorSecondaryBrush`）
- `BusyArea` → 临时 `Panel` + 半透明遮罩 + `ProgressBar`
- 发布栈页：`SukiStackPage` → `ContentControl` + ViewLocator
- 发布二级页（图片分析、封面提示词）有 `BreadcrumbBar`，可回到「发布」

### 验证（已做）

- `dotnet build StarBlogPublisher/StarBlogPublisher.csproj` 通过
- `dotnet run --project .\StarBlogPublisher\ --no-build` 启动后约 8 秒进程仍在（无闪退）

就「能否用 FluentAvalonia 跑通主路径」而言，**迁移主体已落地**。

## 未完成

更准确的表述：剩下的是把应用**做成完整 Fluent 体验**，而不是「还没换成 FluentAvalonia」。

### P0 — 影响观感或日常使用

1. **侧栏图标不统一**  
   侧栏用 Fluent `Symbol`；内容区仍大量 FontAwesome。`PageViewModelBase.Icon` 的 FA 字符串基本闲置。

2. **对话框偏简陋**  
   词云 / 添加分类是通用 `ContentDialog` 包 View；尺寸、滚动、按钮语义、嵌套 Alert 未按 Fluent 习惯打磨；未使用 `TaskDialog`。

3. **Toast 不是 Fluent 组件**  
   `WindowNotificationManager` 与 FA 主题一体感不足。更贴近 Fluent 的方向是 `InfoBar`，或统一封装样式。

4. **标题栏 / 侧栏页眉**  
   Logo 主要在顶栏；`NavigationView` 缺少完整 `PaneHeader`；扩展标题栏命中与观感未系统验收。

5. **二级页信息重复**  
   已有面包屑，图片分析 / 封面提示词页仍自带大标题，层级略乱。

### P1 — 一致性与可维护性

6. **设置页仍是 Tab + Card**  
   可用，但未采用更常见的 Fluent 设置布局（如 `SettingsExpander`）；也未用 `NumberBox` 等 FA 控件。

7. **Busy 遮罩是临时写法**  
   发布中 / 词云生成等为手写遮罩，无统一 Busy 组件。

8. **面包屑未抽象**  
   仅发布页接入；没有可复用的「栈页 + Breadcrumb」宿主，其它二级页需再接。

9. **按钮 / 间距 / 卡片未系统化**  
   去掉 Suki 的 `Basic` / `Accent` 后，各页按钮层级与留白未按 Fluent 规范收齐。

10. **文档与对外说明滞后**  
    - 本文之前，仓库仍以 Suki 重构文档为「当前计划」口吻  
    - `README` / `CLAUDE.md` 几乎未写 FluentAvalonia  
    - 缺少一份正式的 Fluent 迁移 / 打磨计划（可另开文档）

### P2 — 明确不在本轮

11. Avalonia 12 + FluentAvalonia 3.x（含 `Markdown.Avalonia` 兼容）
12. AOT / 打包与 FA 的兼容性回归
13. 系统主题 / 强调色更深接入（如 `PreferSystemTheme`）
14. 自动化 GUI 测试（当前以编译 + 启动冒烟为主）

## 与旧文档的关系

| 文档 | 角色 |
|------|------|
| `docs/2026-09-17-gui-sukiui-refactor.md` | **历史计划**：SukiUI 壳重构的目标与阶段。信息架构（单壳、四级侧栏、Dialog 不进侧栏）仍然有效；组件选型已过时。 |
| `docs/2026-09-18-gui-fluentavalonia-status.md`（本文） | **现状结论**：FA 迁移完成度与剩余工作优先级。 |

建议后续若开打磨计划，新建 `docs/2026-….md`（Fluent 打磨 / 迁移计划），不要在 Suki 文档上继续追加「已改用 FA」的补丁叙述。

## 建议的下一轮顺序（未开工）

1. 侧栏图标统一（FA 或 Fluent Symbol 二选一，删掉闲置字段）
2. Dialog / Toast 打磨（`TaskDialog` / `InfoBar` 或等价封装）
3. 设置页 Fluent 化
4. 更新 README / CLAUDE，并归档或标注 Suki 文档为历史

## 相关提交（参考）

迁移主体：

- `ec9fd31` feat(gui): swap SukiUI for FluentAvalonia theme package
- `2530ac3` feat(gui): rebuild app shell with AppWindow and NavigationView
- `e3e1425` refactor(gui): replace Suki toast/dialog host with FluentAvalonia APIs
- `8e8512b` refactor(gui): restyle pages for FluentAvalonia resources

细节：

- `b8df81f` refactor(gui): move shell actions to sidebar and toast to bottom-right
- `af84979` feat(gui): add breadcrumb navigation for publish stack pages

## 结论

| 层面 | 状态 |
|------|------|
| 换库、能跑、主流程可用 | **基本完成** |
| Fluent 观感与交互打磨 | **未完成（P0 / P1）** |
| 文档与 Avalonia 12 | **未做 / 跟进项** |
