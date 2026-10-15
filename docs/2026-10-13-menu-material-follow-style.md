# 菜单跟随界面材质的验证记录

- 日期：2026-10-13
- 环境：Windows 11 build 26200、.NET 10、Avalonia 12.1.2、FluentAvaloniaUI 3.1.0
- 范围：顶部六组菜单、800 DIP 窄窗口菜单与“最近打开”子菜单。

菜单使用设置中的同一项界面风格，无独立配置。标准恢复原有 Fluent 实色；Mica 与 Acrylic 分别请求对应原生材质。菜单请求不沿用主窗口的兼容材质候选，选定材质不可用时保留主题实色。透明宿主用于圆角区域，不作为材质成功判据；只有实际结果匹配选定材质才使用半透明菜单背景。

菜单沿用原有模板、圆角、边框、阴影、快捷键、勾选与禁用状态。打开期间的风格和明暗变更同步到菜单，关闭时移除材质订阅。

## 用户实测后的视觉修正

初版仅在原生模糊上覆盖 60% 实色菜单底色，未添加 Acrylic 调色、亮度与噪点。用户反馈浅色菜单看起来没有磨砂。彩色底图的真实桌面捕获确认原生模糊生效，但原有叠色在浅色主窗口上让菜单接近实色。

修正后，Acrylic 使用 `ExperimentalAcrylicBorder` 的材质层，清除原有菜单实色背景，降低遮盖并添加调色、亮度和噪点；原生 `AcrylicBlur` 继续提供模糊，材质使用 `BackgroundSource=None`，不重复模糊文字或菜单内容。每个菜单保留自己的稳定材质实例，主题变化更新实例属性。标准、Mica 及高对比度回退隐藏该层。

真实桌面截图，仅捕获确认属于隔离测试进程的菜单区域：

- [设置页浅色 Acrylic](images/material-menu/acrylic-light.png)
- [设置页深色 Acrylic](images/material-menu/acrylic-dark.png)
- [彩色底图浅色 Acrylic](images/material-menu/acrylic-pattern-light.png)
- [彩色底图深色 Acrylic](images/material-menu/acrylic-pattern-dark.png)

设置页截图已检查噪点、背景层次与文字；彩色底图截图已检查原生背景取样及色块边界的模糊。截图通过桌面 DC 捕获，包含 DWM 合成效果，区别于控件树导出 PNG。

## 本机读数

### 2026-10-14 通透度试调

为让背景更透，调整菜单 Acrylic 的背景材质参数：

| 参数 | 提交版 | 试调版 |
| --- | --- | --- |
| 浅色 `TintOpacity` | 0.50 | 0.40 |
| 深色 `TintOpacity` | 0.90 | 0.80 |
| 浅／深 `MaterialOpacity` | 0.12 | 0.08 |

材质回归通过，浅色与深色均返回 `AcrylicBlur`，JSON 中记录的新参数与资源一致。产物位于 `output/desktop-tests/20261013-161946-a1b8bf551c0a41218cd5adb95641b66e/`。真实桌面检查确认设置页文字保持清晰；平缓背景上的变化较细微，彩色底图上的背景色彩更明显。

对照原有截图的试调效果：[浅色设置页](images/material-menu/acrylic-light-translucent.png)、[深色设置页](images/material-menu/acrylic-dark-translucent.png)、[浅色彩色底图](images/material-menu/acrylic-pattern-light-translucent.png)、[深色彩色底图](images/material-menu/acrylic-pattern-dark-translucent.png)。

### 初版与材质层修正读数

产物：[material.json](../output/desktop-tests/20261013-152315-c6f2c9defd654666b51e96b69dc6f90f/material.json)，[report.json](../output/desktop-tests/20261013-152315-c6f2c9defd654666b51e96b69dc6f90f/report.json)。

下表是初版读数；修正后的材质、回退及多级菜单测试产物位于 `output/desktop-tests/20261013-155442-1adbd7434ef344f894e897ba955e652b/`，Acrylic 的 `menuBackground` 为透明、`menuAcrylicLayer` 为 true，实际调色和亮度参数追加到 JSON 中。

| 设置 | 菜单请求 | 实际结果 | 浅色背景 | 深色背景 |
| --- | --- | --- | --- | --- |
| 标准 | Transparent | Transparent | `#FFF9F9F9` | `#FF2C2C2C` |
| Mica | Mica, Transparent | Mica | `#80F9F9F9` | `#802C2C2C` |
| Acrylic | AcrylicBlur, Transparent | AcrylicBlur | `#99F9F9F9` | `#992C2C2C` |

标准菜单背景不透明；Transparent 仅保留原有圆角宿主。材质不匹配、None／Transparent／Blur 结果及高对比度均不得使用半透明菜单背景。

## 已完成检查

- Release 解决方案构建通过。
- `--material` 通过：浅／深 × 三种风格、切回标准、打开菜单时连续切换、模拟高对比度，以及窄窗口三级菜单。
- `--settings` 通过：现有设置草稿、即时预览与撤销流程。
- 常规桌面回归通过，未启用的 WebView 场景保持 skipped。
- 新增菜单策略用例覆盖实际材质不匹配、无模糊透明与高对比度。
- 全量单元测试串行运行 313 / 313 通过。默认并发运行 312 / 313，`ArticleImageCatalogTests.FromHtml_ReadsRenderedMarkdownReferencesNestedImagesAndHtml` 报告 ColorCode 的 `Cannot write to a closed TextWriter`；该用例独立运行通过，异常发生于本次未改动的 `WeChatFormattingService` 代码。串行运行配置与报告位于 `output/menu-material-tests/`，未改变仓库测试并发设置。

## 验证边界

Mica／AcrylicBlur 是原生窗口 API 读数。自动测试额外断言 Acrylic 材质层、原菜单背景透明及回退实色。`RenderTargetBitmap` 不包含 DWM 背景取样或合成材质层；本次改用带独立配置的置顶测试窗口捕获真实桌面菜单，实际截图见上文。用户的壁纸及主窗口内容会影响最终色调。

高对比度通过窗口主题模拟检查。系统关闭透明效果、Windows 10、macOS、Linux、远程桌面及软件渲染未在本次实际验证；其菜单策略与不匹配结果的实色处理有单元测试覆盖。
