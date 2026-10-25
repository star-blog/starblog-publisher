# 测试指南

## 日常自动化测试

在仓库根目录运行：

```powershell
dotnet test StarBlogPublisher.Tests/StarBlogPublisher.Tests.csproj -c Release
```

现有 xUnit 测试覆盖 Core、CLI、服务和 GUI ViewModel，包括文件保存/关闭、布局恢复、命令路由、大纲解析、预览导航模式等。它们不需要打开桌面窗口。GitHub Actions `Tests` 在 PR 和 main/master 推送时运行这些测试、上传 TRX 报告，并编译桌面测试项目。

## 真实窗口回归测试

`StarBlogPublisher.DesktopTests` 是纳入版本控制的独立可执行测试项目，取代 `output/WorkspaceSmoke` 临时脚本作为维护入口。它不会由 `dotnet test` 自动启动。

需要 Windows、.NET 10 SDK 和可交互的桌面会话。运行时会创建自己的窗口并短暂获取焦点；请避免同时操作这个测试窗口。

```powershell
# 常规桌面工作流，预览 DOM 检查明确标记为 skipped
dotnet run --project StarBlogPublisher.DesktopTests -c Release

# 包含真实 WebView2 预览定位检查，需要已安装 WebView2 Runtime
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --webview

# 单独验证设置页，不打开文章或初始化文章预览
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --settings

# 记录窗口材质请求/实际结果与对照截图（隔离配置）
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --material

# 真实 WebView 弹窗及主窗口退出回归（每个退出用例使用独立进程）
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --dialogs

# 查看场景，不启动窗口
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --list
```

按顺序检查同一编辑会话的六个阶段：

| 阶段 | 检查内容 |
| --- | --- |
| workspace-state | 多文档切换、光标/滚动/撤销恢复、导航页面后编辑器仍复用 |
| sidebar-and-menus | 紧凑侧栏、调整宽度、隐藏/显示、菜单分组 |
| focus-and-palette | 编辑器与摘要输入框的命令焦点、替换及撤销、命令面板筛选与执行、大纲源码导航 |
| preview-navigation | 源码滚动后切换分栏/预览、重复点击大纲、分栏导航保持模式 |
| settings-layout | 设置首次打开无意外更改、五类导航、展开宽度稳定、800 DIP 窄窗口、主题预览、界面风格预览与实际效果提示、颜色切换保留风格、草稿保存与撤销、离页确认、写入失败保护 |
| theme-and-close | 强制深浅主题截图、跟随系统（`PreferSystemTheme` / `ActualThemeVariant`）、800 DIP 窄窗口菜单、未保存时取消/丢弃/保存、重新打开 |
| material-shell | 仅 `--material`：浅／深 × 标准／Mica／Acrylic 的主窗口与菜单材质请求、实际结果和背景叠色记录；菜单打开时切换、模拟高对比度、800 DIP 多级子菜单与切回标准；文章侧栏叠色、编辑画布不透明、面板显隐与宽度，截图写入本次运行目录 |

这些阶段是有依赖的端到端工作流，不是彼此独立的单元测试。前一阶段失败后，后续阶段标记为 skipped，避免连锁报错。页面加载和对话框出现采用有超时的条件等待；部分布局、截图和原生焦点仍留有短暂稳定时间。每阶段超时 40 秒，整个进程有 180 秒看门狗。

每次运行创建唯一的 `output/desktop-tests/<时间-随机ID>/`，其中保存测试文章、独立配置、工作区历史、WebView2 用户数据、截图和 `report.json`。不会读取正常应用的账号配置，也不调用发布或 AI 服务。输出目录被 Git 忽略，保留用于排查；确认不需要后可手动删除对应运行目录。

退出码：`0` 表示所有已启用阶段通过，`1` 表示失败或超时，`2` 表示参数或平台不支持。未启用的 WebView 阶段不会计作通过。失败报告包含阶段名、耗时和异常，尽可能另存失败截图；UI 线程卡死时输出 `timeout.txt` 并退出。排查 WebView 加载超时时先检查 Runtime、桌面会话和进程写入权限。

## WebView 弹窗与主窗口关闭回归

Windows 上原生 WebView 会覆盖窗口内部的 Fluent 弹窗。复现条件是预览可见、文章有未保存修改，然后点击主窗口关闭：保存确认框被网页挡住，其他控件被弹窗遮罩阻挡，网页仍能滚动，看起来像关闭卡死。公众号排版和文章预览／分栏都可能触发。

应用内模态弹窗统一通过 `GuiHost.ShowDialogAsync` 显示，保留 `FAContentDialog` 和 `FATaskDialog` 的呈现方式、按钮和返回结果。显示期间暂时隐藏所属窗口的背景 WebView，最后一个弹窗结束后撤销临时属性覆盖；原有可见性绑定、源码模式和网页内容得以保留。系统文件选择器、Toast、菜单和工具提示不经过这个入口。

`--dialogs` 要求真实 WebView2 Runtime，不需要另加 `--webview`，也不与其他运行模式组合。它依次启动 `lifecycle`、`save`、`discard`、`clean` 四个独立测试进程。每个进程沿用隔离配置、文章、工作区和 WebView2 数据目录；汇总报告与日志写入 `output/desktop-tests/dialogs-<时间-随机ID>/`，日志指向各子进程的 `report.json`。每个子进程场景最多 90 秒，父进程 120 秒超时后终止该测试子进程，继续执行其他独立用例。

- `lifecycle`：公众号、本地预览／分栏、站点文章预览；网页内容和滚动位置保留；原本隐藏的 WebView 和可见性绑定恢复；嵌套弹窗、新增背景 WebView、弹窗内 WebView、独立窗口隔离、显示失败和所属窗口关闭清理；命令面板、输入框、TaskDialog、内容弹窗关闭订阅清理、查找焦点恢复、设置离页决策及关闭异常后的重试。
- `save`、`discard`：真正调用主窗口 `Close()`，验证取消后保留修改并恢复预览、重复关闭不叠加确认框，然后连续处理两篇未保存文章，最终关闭窗口并检查保存／不保存的磁盘结果。
- `clean`：公众号 WebView 已加载且没有修改时直接关闭主窗口。

这些自动断言验证实际控件状态和 WebView DOM，按钮通过路由事件触发。原生遮挡还需要桌面验收：在公众号及预览／分栏页面点击标题栏关闭，确认保存框可见且可用鼠标点击；取消后网页可滚动，保存／不保存后窗口退出。检查一次最大化状态。使用系统级原生窗口截图确认弹窗与网页的层叠，不能用 `RenderTargetBitmap` 截图替代这一步。

## 覆盖边界与新增用例

状态规则优先放进 xUnit；需要真实布局、焦点、编辑器或 WebView 行为的回归，加入 `WorkspaceScenarios.cs` 对应阶段。WebView 必须等页面加载完成再读取 DOM，不能只检查 ViewModel 就声称页面已滚动。

`SettingsViewModelTests` 覆盖草稿更改检测、AI 方案与公众号账号切换、主题与界面风格预览撤销、九种外观组合、颜色切换保留风格、材质状态提示不脏草稿、校验失败和离页决策。设置使用独立草稿，只有保存成功才更新运行中的配置；模型目录仅在主动获取或打开目录时请求。桌面测试使用占位凭据，保存检查只写入本次运行的独立配置。

当前测试调用控件 API、命令和路由事件，不覆盖真实鼠标命中、系统窗口拖动或跨平台原生差异。PNG 供人工检查，并非像素基线断言；原生 WebView 内容使用 JavaScript 验证，不能靠 Avalonia 截图证明。桌面测试暂不在托管 CI 自动执行，因为它要求可交互桌面和原生浏览器环境。

菜单材质在 `material.json` 中追加 `menuHint`、`menuActual`、`menuBackground`、`menuAcrylicLayer`、`menuTintOpacity` 和 `menuMaterialOpacity`，并生成 `*-menu.png`。Acrylic 成功时原有菜单实色背景必须透明，由独立材质层提供调色、亮度与噪点；实色回退时材质层隐藏。菜单 PNG 由 `RenderTargetBitmap` 绘制，不包含 DWM 背景取样及合成材质层；API 返回 Mica／AcrylicBlur 不等于已完成桌面合成观感的人工验收。原生桌面截图及验证边界见[菜单材质记录](2026-10-13-menu-material-follow-style.md)。
