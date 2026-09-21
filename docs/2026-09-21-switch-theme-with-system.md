# 跟随系统主题（Light / Dark / System）实现计划

## Goal

为 StarBlogPublisher（Avalonia 12.1 + FluentAvalonia 3.1）增加**三态外观偏好**：跟随系统 / 浅色 / 深色，并在 Windows、macOS、Linux 上都能随 OS 主题变化自动切换。现有「深色主题」布尔开关与侧栏一键切换语义一并升级。

## 已确认的产品决策

| 项 | 选择 |
|---|---|
| 设置 UI | 三态：跟随系统 / 浅色 / 深色 |
| 侧栏主题按钮 | 浅色 ↔ 深色；若当前是「跟随系统」，点一次先**退出跟随**并切到**当前实际主题的反色**；回到「跟随系统」只在设置里选 |
| 平台范围 | Desktop：`win-x64` / `linux-x64` / `osx-x64`（与现有打包一致） |

## 现状（基线）

- `AppSettings.IsDarkTheme: bool`（默认 `false`）持久化到 `%AppData%\StarBlogPublisher\settings.json`，便携导出也带该字段。
- `App.axaml`：`RequestedThemeVariant="Light"`，`FluentAvaloniaTheme PreferSystemTheme="False"`。
- 启动时按 `IsDarkTheme` 强制设 `ThemeVariant.Light|Dark`（`App.axaml.cs`）。
- 壳层 `MainWindowViewModel.ApplyTheme` / `PreviewTheme`、设置页 Toggle、侧栏 `ToggleTheme`、命令面板 `view.theme` 都围绕 bool。
- 已有消费「实际是否深色」的路径：`PublishViewModel` 预览 HTML class、`MarkdownEditorView`（已监听 `ActualThemeVariantChanged`）。
- 归档文档曾把「系统主题 / PreferSystemTheme」列为 P2，现在正式落地。

## 技术方案（跨平台核心）

### 1. 用 Avalonia 原生三态，而不是自写平台钩子

Avalonia 已通过 `IPlatformSettings` 在 Win / macOS / Linux 解析系统浅深色：

- `Application.RequestedThemeVariant = ThemeVariant.Default` → 跟随系统，并在 OS 变更时更新 `ActualThemeVariant`
- `ThemeVariant.Light` / `ThemeVariant.Dark` → 强制覆盖

**不要**再手写 Win32 注册表 / macOS NSAppearance / DBus；只在文档与冒烟清单里注明各平台探测边界。

### 2. FluentAvalonia 配合方式

参考 FluentAvalonia 自带 Settings 样例：

| 用户选择 | `RequestedThemeVariant` | `FluentAvaloniaTheme.PreferSystemTheme` |
|---|---|---|
| 跟随系统 | `Default` | `true`（顺带覆盖 HighContrast / KDE 等 FA 额外逻辑） |
| 浅色 | `Light` | `false` |
| 深色 | `Dark` | `false` |

启动与运行时切换都走同一套 helper，避免 FA 在 `PreferSystemTheme=true` 时覆盖掉用户强制的 Light/Dark。

强调色保持现状：`PreferUserAccentColor="True"`（与主题模式独立）。

### 3. 设置模型：`ThemeMode` + 兼容旧 `IsDarkTheme`

在 Core 增加枚举（建议放 `StarBlogPublisher.Core/Models` 或 Settings 旁）：

```csharp
public enum ThemeMode {
    System = 0,
    Light = 1,
    Dark = 2
}
```

`AppSettings`：

- 新增 `ThemeMode ThemeMode { get; set; } = ThemeMode.System;`（**新安装默认跟随系统**）
- 保留 `bool IsDarkTheme` 作为**兼容字段**：
  - **读**：若 JSON 有 `themeMode`/`ThemeMode` 则用之；否则用旧 `IsDarkTheme`：`true→Dark`，`false→Light`（**老用户外观不变，不会突然变成跟随系统**）
  - **写**：同时写 `ThemeMode`，并写派生的 `IsDarkTheme = (ThemeMode == Dark)`，方便旧工具/旧测试读文件；或文档说明 `IsDarkTheme` 仅在非 System 时有意义，System 时写当前 Actual 亦可——推荐**写派生值且 System 时写 `false` 或写 Actual 快照**；更干净的做法是 **System 时 `IsDarkTheme` 仍按上次强制值或固定 false，真正 UI 以 ThemeMode 为准**。实现时选：`IsDarkTheme` 仅表示「强制深色」，System/Light → false，Dark → true（兼容旧布尔语义）。

便携导入导出（`AppSettingsPortableTransfer` / payload）同步增加 `ThemeMode`，并对缺字段走同样迁移规则。

### 4. 主题应用中枢（GUI）

新增小组件（建议 `StarBlogPublisher/Services/AppThemeService.cs` 或放在现有 `GuiHost` 旁）：

```text
Apply(ThemeMode mode)
  → 设置 RequestedThemeVariant
  → 设置 FluentAvaloniaTheme.PreferSystemTheme
  → （可选）不直接碰 IsDarkTheme 持久化；由壳层负责 Save

ResolveEffectiveIsDark()
  → Application.Current.ActualThemeVariant == ThemeVariant.Dark
```

壳层职责拆分：

| API | 含义 |
|---|---|
| `ThemeMode`（偏好） | 用户选择，可持久化 |
| `IsDarkTheme`（有效） | 当前实际浅/深，驱动图标、预览 HTML、命令勾选 |
| `ApplyTheme(ThemeMode)` | 应用 + 写设置 + 同步 Settings 草稿 |
| `PreviewTheme(ThemeMode)` | 仅改运行时主题，不持久化（设置页即时预览） |
| `ToggleTheme()` | 见下方侧栏语义 |

订阅 `Application.ActualThemeVariantChanged`（在 MainWindow 或壳 VM 初始化后）：

- 更新 `IsDarkTheme`（有效值）
- `RefreshFooterNavItems()`
- `Workspace.Documents` → `RefreshPreviewForThemeChange()`

这样在「跟随系统」下 OS 切换时，编辑器高亮（已有）、预览 HTML、侧栏图标都会跟上。

### 5. UI / 交互

**设置 → 外观**（`SettingsView.axaml`）

- 去掉「深色主题」`ToggleSwitch`
- 换成三选一：`ComboBox` 或一组 `RadioButton`（推荐 Radio，与同页「默认服务/自定义服务」一致）
  - 跟随系统 / 浅色 / 深色
- 提示文案：`即时预览，保存后保留；撤销可恢复。跟随系统时随操作系统浅色/深色自动切换。`

**SettingsViewModel**

- 草稿字段：`ThemeMode`（替换或并存于 `IsDarkTheme`）
- Dirty 指纹纳入 `ThemeMode`
- `OnThemeModeChanged` → `_shell.PreviewTheme(ThemeMode)`（保留现有「预览不落盘、Cancel 还原」行为）
- Save / Cancel / Backup import 走 `ApplyTheme(ThemeMode)`

**侧栏底部主题项 + 命令面板**

- Tooltip：强制浅/深时仍「切换到深色/浅色」；跟随系统时改为「切换到浅色/深色（将退出跟随系统）」之类
- 图标仍按**有效** `IsDarkTheme`（太阳/月亮）
- `ToggleTheme` 算法：

```text
if ThemeMode == System:
    next = ActualIsDark ? Light : Dark   // 退出跟随，切到反色
else if ThemeMode == Light:
    next = Dark
else:
    next = Light

若在设置页编辑中 → 只改草稿 ThemeMode（预览）
否则 → ApplyTheme(next) 并持久化
```

### 6. 平台注意点（文档 + 手工冒烟，不写平台分支代码）

| 平台 | 机制（Avalonia） | 验证要点 |
|---|---|---|
| Windows 10/11 | 系统应用浅/深色；标题栏随 `RequestedThemeVariant`（Win11 更完整） | 设置 ↔ OS 切换实时；强制模式不受 OS 影响 |
| macOS | Appearance | 同上；注意 `osx-x64` / `osx-arm64` 打包冒烟 |
| Linux | FreeDesktop `color-scheme` 等 | GNOME/KDE 等支持较好；部分 WM 可能一直报 Light——属平台限制，UI 仍可强制浅/深 |

HighContrast（Windows）：`PreferSystemTheme=true` 时 FA 可切入 HighContrast；本计划**不单独做第四态 UI**，跟随系统即可吃到；若后续要「永不进高对比」，再加开关。

## 文件改动清单（预计）

**Core**

- `StarBlogPublisher.Core/Models/ThemeMode.cs`（新）
- `StarBlogPublisher.Core/Services/AppSettings.cs`（属性、Snapshot、序列化迁移）
- `StarBlogPublisher.Core/Services/AppSettingsPortableTransfer.cs`（payload 字段）

**GUI**

- `StarBlogPublisher/Services/AppThemeService.cs`（新，可选但推荐）
- `StarBlogPublisher/App.axaml`（启动默认可改 `Default`；运行时仍由代码覆盖）
- `StarBlogPublisher/App.axaml.cs`（按 `ThemeMode` 启动）
- `StarBlogPublisher/ViewModels/MainWindowViewModel.cs`（ThemeMode、Apply/Preview、Toggle、ActualTheme 订阅）
- `StarBlogPublisher/ViewModels/MainWindowViewModel.Commands.cs`（命令文案）
- `StarBlogPublisher/ViewModels/SettingsViewModel.cs` + `Editing.cs` + `Backup.cs`
- `StarBlogPublisher/Views/Pages/SettingsView.axaml`
- `StarBlogPublisher/Converters/BoolToThemeTextConverter.cs`（改成 ThemeMode 文案转换器，或删除若不再用）

**测试**

- `AppSettingsCompatibilityTests`：旧 JSON 仅有 `IsDarkTheme` → 映射到 Light/Dark；新 JSON `ThemeMode`
- `AppSettingsPortableTransferTests`：往返 ThemeMode
- `SettingsViewModelTests.ThemePreview_*`：改为 ThemeMode 预览/Cancel
- `DesktopTests.WorkspaceScenarios.ThemeAndClose`：`ApplyTheme(ThemeMode.Dark/Light)`；可选加 System 冒烟（桌面环境允许时）

**文档（可选短文）**

- `docs/2026-09-21-system-theme-follow.md`：行为说明 + 平台冒烟清单（与现有 GUI 文档风格一致）

## 实现顺序

1. **Core 模型与序列化迁移**（`ThemeMode`、读旧写新、便携字段）+ 单测  
2. **`AppThemeService` + App 启动应用**  
3. **壳层 Apply/Preview/Toggle + ActualThemeVariantChanged**  
4. **设置页 UI 与草稿/Save/Cancel/导入**  
5. **更新单元测试与 DesktopTests**  
6. **本机冒烟**：强制浅/深、跟随系统、OS 切换、设置预览与撤销、侧栏退出跟随  

## 测试计划

- 单元：兼容加载、便携往返、设置预览不落盘 / Cancel 还原  
- Desktop：`ThemeAndClose` 覆盖强制浅深；若环境可测，短暂设 System 并断言 `RequestedThemeVariant == Default`  
- 手工（多平台各一次）：  
  1. 跟随系统 → 改 OS 主题 → UI/预览/编辑器高亮同步  
  2. 强制浅或深 → 改 OS → 应用不变  
  3. 跟随下点侧栏 → 变为强制反色且设置草稿/已保存模式正确  
  4. 设置改三态即时预览 → Cancel 回到保存值  

## 风险与边界

- **Linux 部分桌面环境**可能不报告 color-scheme：跟随系统会停在浅色；强制模式仍可用。  
- **FA PreferSystemTheme 与强制模式冲突**：必须在切 Light/Dark 时关掉 PreferSystemTheme。  
- **`IsDarkTheme` 双字段语义**：旧测试与配置导入要明确迁移规则，避免「System + IsDarkTheme=true」歧义。  
- **设置页编辑中 OS 主题变化**：跟随系统且草稿也是 System 时，Actual 变化应更新有效 `IsDarkTheme`，但不要把草稿标脏。  
- 本仓库无浏览器验证路径；GUI 以 DesktopTests + 本机运行冒烟为准。

## 非目标（本轮不做）

- 独立 HighContrast 选项 UI  
- 自定义强调色面板  
- 浏览器 / 移动端 Avalonia 目标  
- 按窗口分别设主题（`ThemeVariantScope`）
