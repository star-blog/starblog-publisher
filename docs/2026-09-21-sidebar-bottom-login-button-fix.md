# 侧栏底部登录入口不可见 — 修复方案

## 问题诊断

截图里侧栏是 **LeftCompact 收起态**（约 48px 宽，只显示图标）。登录/注销其实还在，但被挤没了。

根因在 `StarBlogPublisher/Views/MainWindow.axaml`：

| 配置 | 现状 | 影响 |
|------|------|------|
| `PaneDisplayMode="LeftCompact"` | 常驻紧凑侧栏 | 只留图标轨 |
| `IsPaneToggleButtonVisible="False"` | 汉堡按钮隐藏 | 用户无法展开侧栏 |
| `IsPaneOpen` 默认 `false` | 启动即收起 | 永远看不到展开态文案 |
| `PaneFooter` 用 **横向** `StackPanel` | 主题 / 登录 / 注销并排 | 48px 里只能露出第一个（月亮图标），登录被裁切且无法点击 |

主题切换仍可见；登录/注销被裁在可视区域外。文章属性面板里虽有「需要登录时」的登录按钮，但不能替代壳级登录/注销入口。

## 目标（已确认）

重新设计主题、登录/注销按钮，**视觉与交互对齐侧栏顶部导航图标**（竖排、图标按钮、紧凑态可用），而不是简单把侧栏改回可展开。

## 推荐实现：`FooterMenuItems`

FluentAvalonia / WinUI 的标准做法：用 `FANavigationView.FooterMenuItems` 放操作项，而不是在 `PaneFooter` 里塞横向自由布局。

`FooterMenuItems` 里的 `FANavigationViewItem` 会：

- 自动跟顶部菜单一样竖排
- Compact 下只显示图标 + tooltip
- 命中区域、间距、hover/press 样式与顶部一致

`PaneFooter` 留给真正的自定义内容；操作型图标应走 `FooterMenuItems`。

### 具体改动

**1. `MainWindow.axaml`**

- 删除现有 `PaneFooter` 横向按钮组（或留空/`PaneFooter` 不用）
- 增加两个 `FANavigationViewItem`：

```xml
<ui:FANavigationView.FooterMenuItems>
  <ui:FANavigationViewItem x:Name="ThemeNavItem"
                           Content="主题"
                           SelectsOnInvoked="False"
                           Tag="theme" />
  <ui:FANavigationViewItem x:Name="AccountNavItem"
                           Content="登录"
                           SelectsOnInvoked="False"
                           Tag="account" />
</ui:FANavigationView.FooterMenuItems>
```

- `SelectsOnInvoked="False"`：点击不会抢走顶部页面的 `SelectedItem`
- 绑定 `ItemInvoked`（或在 code-behind 里按 `Tag` 分发）到现有 `ToggleThemeCommand` / `LoginCommand` / `LogoutCommand`

**2. `MainWindow.axaml.cs`（轻量同步）**

Footer 项的图标/文案随状态变化，XAML 里 `IconSource` 不好直接绑两套图标，用 code-behind 同步即可：

- `IsDarkTheme` → Theme 项图标 `WeatherMoon` / `WeatherSunny`，Tooltip「切换到深色/浅色」
- `IsLoggedIn` → Account 项图标 `Person` / `SignOut`，Content/Tooltip「登录」/「登出」
- 在 `DataContextChanged` 与对应 `PropertyChanged` 时刷新

点击分发：

- `Tag == "theme"` → `ToggleThemeCommand`
- `Tag == "account"` → 未登录 `LoginCommand`，已登录 `LogoutCommand`

**3. 不改动**

- `MainWindowViewModel` 的登录/主题命令与业务逻辑
- `PaneDisplayMode` / 隐藏汉堡按钮（保持常收起导航壳）
- Core / CLI / MCP

## 备选（不采用，除非 FooterMenuItems 有兼容问题）

**竖排 `PaneFooter` 按钮**：把 `Orientation` 改成 `Vertical`，按钮 `Height=40` 居中图标。改动最小，能立刻点到登录，但不会自动获得与顶部 `FANavigationViewItem` 完全一致的选中/hover chrome。仅作 Fallback。

**恢复可展开侧栏**：重新显示 toggle、允许 `IsPaneOpen`。能露出旧横向 footer，但与「固定收起 + 图标风格对齐」的目标不一致，本次不做。

## 验证

1. `dotnet build StarBlogPublisher/StarBlogPublisher.csproj`
2. 启动 GUI：侧栏底部应看到 **主题图标** 与 **登录/账户图标**（竖排，与顶部对齐）
3. 未登录：点账户图标打开登录；登录成功后图标变为登出
4. 主题切换图标随深浅色变化，且不改变当前选中的页面（工作区/设置等）
5. Tooltip 在收起态可读（登录 / 登出 / 主题）

## 风险与注意

- `FooterMenuItems` 的 `ItemInvoked` 与页面 `SelectedItem` 是分开的；务必 `SelectsOnInvoked="False"`，避免误选中 footer 项导致内容区空白
- 若 FA 3.1 对 Footer 项的 Invoked 行为与 WinUI 有差异，再退回竖排 `PaneFooter` Fallback
- 桌面 GUI 无浏览器可测；以启动后手动点击 + build 为准
