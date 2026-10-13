# Mica / Acrylic 界面风格开发计划

- 日期：2026-10-13
- 状态：阶段 0–4 已实施；阶段 5 待实施
- 范围：GUI 外观、共享配置模型及相关测试；不修改发布业务、CLI 命令或 MCP 协议行为。
- 技术基线：当前工作区为 .NET 10、Avalonia 12.1.2、FluentAvaloniaUI 3.1.0，以实际项目依赖为准。

## 1. 目标与产品约定

在已有明暗主题上增加独立的材质风格，最终设置方式为：

```text
颜色模式：跟随系统 / 浅色 / 深色
界面风格：标准 / Mica / Acrylic
```

两个维度独立，共形成九种偏好组合。跟随系统解析为当前实际浅色或深色；切换颜色模式不改变界面风格，切换界面风格不改变颜色模式。

### 1.1 风格定义

| 风格 | 预期效果 | 约定 |
| --- | --- | --- |
| 标准 | 现有实色 Fluent 界面 | 默认值；不启用背景材质，尽量保持原有视觉表现 |
| Mica | 柔和、基于壁纸色彩的云母背景 | Windows 11 优先原生 Mica；不承诺实时透出后方窗口 |
| Acrylic | 更明显的模糊半透明背景 | 优先 AcrylicBlur，配合浅／深色叠色；不等于整个窗口降低透明度 |

默认保持“跟随系统 + 标准”。旧配置缺少风格字段时使用标准，保留其已有颜色模式，不让老用户升级后突然变成半透明界面。

### 1.2 设计原则

1. 外壳有材质，内容有承托，文字与图标保持不透明。
2. 优先使用 Avalonia 的窗口材质能力，不替换 `FAAppWindow`，不引入 WinUI 运行时。
3. 材质由窗口底层提供，各区域通过背景画刷叠色形成层次；不为每张卡片重复创建背景模糊。
4. 不通过降低窗口或页面整体 `Opacity` 实现风格，不对整棵控件树施加模糊。
5. 不支持材质时安全回退为实色；用户的风格偏好不因降级而被改写。
6. 第一轮先完成窗口外壳、主导航与设置页；第二轮再优化文章工作区辅助区域。

### 1.3 本次不做

- NativeWebView 透明化，或更换 Markdown／微信预览控件。
- Mica Alt、自定义壁纸、背景图片、透明度滑块、独立浮层 Acrylic 等额外外观选项。
- 通过桌面截图、持续采样或自定义着色器模拟原生材质。
- 大幅调整页面布局、编辑器功能、发布流程或现有窗口交互。
- 在所有操作系统上复刻完全一致的 Windows 材质。

## 2. 现有实现与改动入口

| 文件／区域 | 当前情况 | 计划用途 |
| --- | --- | --- |
| `StarBlogPublisher.Core/Models/ThemeMode.cs` | System / Light / Dark，与旧 `IsDarkTheme` 兼容 | 保持语义不变，不把材质加入该枚举 |
| `StarBlogPublisher.Core/Services/AppSettings.cs` | 配置加载、快照、保存及兼容转换 | 纳入独立风格字段 |
| `StarBlogPublisher.Core/Services/AppSettingsJsonContext.cs` | JSON 源生成上下文 | 覆盖新增字段类型 |
| `StarBlogPublisher.Core/Services/AppSettingsPortableTransfer.cs` | 可移植配置导入导出及独立 JSON 上下文 | 保留风格偏好和旧备份兼容 |
| `StarBlogPublisher/Services/AppThemeService.cs` | 应用颜色模式并解析实际明暗 | 保持现有颜色模式职责，与材质应用协调 |
| `StarBlogPublisher/Views/MainWindow.axaml(.cs)` | `FAAppWindow`、扩展标题栏、自定义菜单与拖动区 | 绑定材质状态，保留窗口交互 |
| `StarBlogPublisher/App.axaml(.cs)` | Fluent 主题、公共样式、启动时应用主题 | 加载材质资源，避免启动闪烁 |
| `StarBlogPublisher/ViewModels/MainWindowViewModel.cs` | 主题预览、保存、系统主题变化、预览刷新 | 管理外观偏好；材质单独变化不触发正文预览重建 |
| `StarBlogPublisher/ViewModels/SettingsViewModel*.cs` | 草稿、即时预览、脏状态、保存／撤销、备份 | 将两项外观偏好纳入完整草稿生命周期 |
| `StarBlogPublisher/Views/Pages/SettingsView.axaml` | 常规页中的外观单选项 | 两组独立选项与降级说明 |
| `StarBlogPublisher/Views/Pages/ArticleWorkspaceView.axaml` 等 | 侧栏、标签栏和面板存在独立实色背景 | 第二轮按区域优化分层 |
| `StarBlogPublisher/Views/Controls/MarkdownEditorView.axaml(.cs)` | AvaloniaEdit + 原生 NativeWebView | 编辑画布保持稳定，保留既有文档与焦点状态 |
| `StarBlogPublisher/Services/MarkdownPreviewService.cs` | 预览 HTML 有独立浅／深实色背景 | 本计划不将其背景透明化 |

注意：`FAAppWindow` 默认窗口背景、`FANavigationView` 的面板／内容背景和页面实色背景可能遮住底层材质。不能把“设置 TransparencyLevelHint”当作完成整个视觉改造。

## 3. 配置与 GUI 架构

### 3.1 配置模型

拟新增纯配置枚举 `InterfaceStyle`，取值为 `Standard / Mica / Acrylic`，以及同名配置字段。具体声明与命名在实现时保持项目规范一致。

- Core 只保存用户偏好，不引用 Avalonia、窗口句柄或平台材质类型。
- 覆盖运行配置、持久化快照、兼容加载模型、可移植备份及两套 JSON 源生成上下文。
- 缺失／null 字段按标准处理；未定义的数值枚举按标准规范化。
- 对未知字符串枚举验证现有序列化行为，明确采用安全降级还是受控报错；不得让错误值进入 GUI 材质应用。若需要定制转换器，必须保持源生成／AOT 兼容。
- 保持 `ThemeMode` 优先于旧 `IsDarkTheme` 的现有兼容规则，材质不参与该布尔值计算。
- 跨平台导入时保留请求的风格，仅在运行时降级；避免在不支持材质的平台保存后丢失偏好。
- 遵循现有备份版本机制，评估新增可选字段是否需要版本变更，不无故破坏旧备份读取。

### 3.2 GUI 职责分工

- `AppThemeService`：继续管理颜色模式与实际明暗。
- 拟新增 `WindowMaterialService`（GUI 层）：集中管理窗口材质请求、实际结果、背景回退、主题变化与订阅释放。
- 将候选材质选择和回退判断整理成可单元测试的 GUI 策略逻辑；命名可在实施时调整。
- 壳层与设置 ViewModel 管理偏好和预览，不直接操作原生 API。
- MainWindow 负责把窗口实例交给材质服务，并在窗口关闭时清理订阅。

运行时区分三类状态：用户请求的风格、平台报告的材质结果、经可访问性策略处理后的最终呈现。实际效果和降级原因不写入用户配置。

保持 `RequestedThemeVariant / ActualThemeVariant` 的原有浅／深语义，不用新增自定义主题变体取代明暗判断，以免影响编辑器配色和 HTML 预览。

### 3.3 资源分层

拟新增 `StarBlogPublisher/Themes/MaterialResources.axaml`，按实际浅／深主题及界面风格组织应用级语义画刷：

- `ShellBackgroundBrush`：窗口／外壳底色。
- `SidebarBackgroundBrush`：导航与文件侧栏。
- `WorkspaceCanvasBrush`：稳定的编辑内容画布。
- `InspectorBackgroundBrush`：文章属性与辅助面板。
- `WorkspaceChromeBrush`：标签栏、工具栏、任务栏和状态栏。
- `MaterialFallbackBrush`：当前主题下的不透明回退底色。

标准风格映射到原有资源；Mica 与 Acrylic 使用不同的叠色强度。具体颜色／Alpha 值以真实窗口对照验证为准，不在计划阶段固定。

只在必要范围内覆盖 FluentAvalonia 导航资源或模板样式，不全局将 `SolidBackgroundFillColor*` 改为透明。不复制完整第三方控件模板作为首选方案。

## 4. 平台策略与安全降级

### 4.1 候选顺序

| 请求风格 | 首选与候选顺序 | 最终回退 |
| --- | --- | --- |
| 标准 | 关闭材质请求并恢复标准背景 | 原有实色界面 |
| Mica | Mica → AcrylicBlur → Blur | 当前浅／深主题的实色底色 |
| Acrylic | AcrylicBlur → Blur | 当前浅／深主题的实色底色 |

Mica 不可用而改用 Acrylic／Blur 时必须明确提示实际效果；Acrylic 不反向降级为 Mica，避免把两种选项混为一谈。最终呈现由实际获得的材质和回退策略决定，而非候选列表推断。

### 4.2 平台预期

| 环境 | 计划行为 |
| --- | --- |
| Windows 11 | 验证原生 Mica 与 AcrylicBlur，以及浅／深切换 |
| Windows 10 | 原生 Mica 不可用；按候选顺序尝试 AcrylicBlur，失败则实色 |
| macOS | 使用平台可用的原生 Blur 作为兼容效果，明确不是 Windows 原生 Mica／Acrylic |
| Linux X11 + KWin | 尝试平台支持的 Blur，验证桌面合成状态变化 |
| 其他 Linux／Wayland 环境 | 以实际后端能力为准；不保证模糊，必须安全实色回退 |
| 高对比度／系统要求禁用透明 | 优先可访问性，使用相应实色背景和系统配色 |
| 软件渲染、驱动异常、远程桌面等 | 不保证材质；验证回退与窗口可读性，不强制单一渲染后端 |

### 4.3 必须验证的技术细节

1. `TransparencyLevelHint` 是请求，不是效果保证；监听 `ActualTransparencyLevel`，允许窗口打开后及运行期间重新评估。
2. 材质请求失败可能只得到普通 `Transparent`，而不是 `None`。本功能不接受“无模糊的裸透明”作为成功回退，必须绘制不透明底色。
3. 不只依赖 `TransparencyBackgroundFallback`：核实当前 `FAAppWindow` 平台模板是否正确应用它，必要时显式控制窗口／外壳背景。
4. 从 Mica／Acrylic 切回标准时，同时关闭材质、恢复背景和资源层次；验证原生效果不会残留，不能只把前景涂成实色就算通过。
5. 保留现有默认渲染后端及其 fallback；第一阶段不引入自定义 P/Invoke 或强制 WinUIComposition。
6. 检查系统透明效果开关、高对比度等通知的实际支持。平台报告材质可用也不等于肉眼可见，不能只靠枚举值宣布通过。
7. 处理窗口打开、失焦、最大化／还原、主题切换和关闭；订阅须可释放，避免重复挂接与循环更新。

设置页使用简洁、可本地化的状态说明，例如“当前使用原生 Mica”“当前使用兼容模糊效果”“当前环境不支持，已使用实色背景”。文案由运行时结果生成，不能仅检查操作系统名称。

## 5. 分阶段实施

各阶段以独立、可验证的增量推进；目前以下任务均未开始。

### 阶段 0：基线记录与技术验证

- [x] 使用隔离测试配置，记录标准风格的浅／深截图、窗口交互和设置草稿行为。
- [x] 以现有 `FAAppWindow` 做最小材质验证，不替换窗口、不改发布业务。
- [x] 在真实 Windows 11 桌面分别验证 Mica 与 AcrylicBlur，记录请求值、实际值、渲染环境和截图。
- [x] 找出窗口、导航及内容背景遮挡，确定最小资源覆盖范围。
- [x] 验证材质之间切换、切回标准、不可用时实色回退，特别检查深色回退无白底闪烁。
- [x] 确认高对比度／关闭透明效果的检测方式与不支持平台的策略。

**交付与门槛：** 最小验证结果和明确的降级路径。若当前后端存在切换残留或透明泄漏，先解决／记录阻塞，不继续铺开页面样式。临时验证代码不作为正式交付遗留。

### 阶段 1：独立风格模型与配置兼容

- [x] 新增 `InterfaceStyle` 及默认标准值，不更改 `ThemeMode` 的已有取值。
- [x] 接入配置加载、快照、保存、兼容转换与 JSON 源生成。
- [x] 接入可移植配置导入导出；补充缺失字段和异常值处理。
- [x] 扩展 `AppSettingsCompatibilityTests`、`AppSettingsPortableTransferTests`。

**验收：** 九种偏好组合能够正确保存／恢复；旧配置和旧备份仍按原颜色模式加载，风格默认标准；跨平台降级不改写配置。

### 阶段 2：窗口外壳、主导航与材质服务

- [x] 实现集中材质服务及可测试的候选／回退策略。
- [x] 加载语义资源并提供标准、Mica、Acrylic 的浅／深映射。
- [x] 接入窗口启动与系统主题变化，避免不必要的重复更新及启动闪烁。
- [x] 调整标题栏、主导航及设置页底层背景，让材质可见。
- [x] 保留菜单、拖动区、标题栏按钮、命令搜索和窄窗口布局。
- [x] 对现有编辑区与颜色敏感画布做底色保护，避免仅修改外壳就使正文意外裸透明；完整工作区分层留到阶段 4。
- [x] 实现真实材质结果和兼容状态反馈，完成订阅清理。

**验收：** 标准风格无明显视觉回归；两种材质在支持环境下可区分且可见；不支持时无裸透明、黑底／白底错误或不可读文字；切回标准不残留效果。

### 阶段 3：设置 UI 与完整预览生命周期

- [x] 设置页提供“颜色模式”和“界面风格”两组独立单选项。
- [x] 添加 Mica／Acrylic 简短说明及当前实际效果提示，沿用 Fluent 控件、Compiled Bindings 和现有 MVVM 模式。
- [x] 将风格加入草稿初始化、变更通知、脏状态集合与 fingerprint。
- [x] 统一应用两项外观偏好：支持即时预览、保存、撤销、离页确认、恢复默认及配置导入后的同步。
- [x] 保存失败时不更新持久化配置、不误清除脏状态，允许继续编辑或撤销；遵循现有草稿行为。
- [x] 左下角颜色切换只修改颜色模式，不重置当前风格；处理设置页有未保存草稿时的同步规则。
- [x] 仅材质变化不重建 NativeWebView、不重新生成文章 HTML、不改变文档、光标、滚动或撤销历史。
- [x] 扩展 `SettingsViewModelTests`、壳层相关测试和桌面设置场景。

**验收：** 九种偏好组合可操作；保存后重启一致，撤销／离页放弃可同时恢复两项偏好；降级提示不影响草稿脏状态。设置 UI 在 800 DIP 宽窗口仍可完整使用。

**第一轮交付边界：** 阶段 0–3 完成后，交付标准／Mica／Acrylic 外壳及设置能力；正文和预览仍使用稳定内容画布，不等待整个工作区材质优化。

### 阶段 4：文章工作区辅助区域分层

- [x] 调整文件侧栏、文章标签栏、工具栏、文章属性面板、任务面板与状态栏，按用途使用语义画刷。
- [x] 保持 Markdown 源码编辑区不透明或接近不透明，检查语法高亮、行号、当前行、选区和键盘焦点对比度。
- [x] Markdown／微信预览继续使用独立实色画布，不修改 NativeWebView 的透明策略。
- [x] 检查站点文章列表与编辑页、设置卡片等复用区域；封面／图片画布保留稳定底色。
- [x] 检查 BusyOverlay、ContentDialog、命令面板、菜单／Flyout 与原生 WebView 的叠放和遮挡，不擅自改变已有对话框遮罩策略。
- [x] 保持侧栏调整宽度、面板显隐、专注模式及多文档编辑状态不变。

**验收：** 外围与内容层次清楚，正文阅读不受桌面背景变化干扰；源码／分栏／预览切换和既有工作流无回归。不给弹窗新增独立原生 Acrylic 行为。

### 阶段 5：跨平台回归、性能与文档收尾

- [ ] 按测试矩阵验证支持环境和降级环境，明确未验证的系统／后端。
- [ ] 检查 Windows 多 DPI、跨屏、最大化／还原、失焦、窗口拖动及连续风格切换。
- [ ] 与标准风格对比启动、滚动、输入与缩放的耗时／资源表现；记录设备和测量方式，不预设所有硬件都能保持相同帧率。
- [ ] 检查重复主题／风格切换后是否出现重复订阅、窗口资源泄漏或 WebView 重建。
- [ ] 更新 `docs/testing.md` 与相关用户文档，说明风格区别、平台兼容和回退行为。

**验收：** 有测试结果与环境说明，不以单元测试或 CI 构建代替原生材质验证。没有测试条件的平台须明确标记待验证，不能标记为通过。

## 6. 测试与验证矩阵

### 6.1 自动化测试

| 类别 | 必须覆盖 |
| --- | --- |
| 配置兼容 | 旧 `IsDarkTheme`、现有 `ThemeMode`、缺失风格、异常枚举、九种组合 round-trip |
| 备份 | 新风格导入导出、旧备份、跨平台保留偏好、受控失败不污染运行配置 |
| 材质策略 | 两种风格不同候选顺序、None／Transparent 不视为磨砂成功、可访问性实色策略 |
| 设置草稿 | 即时预览、保存、撤销、离页保存／放弃／取消、恢复默认、写入失败、导入同步 |
| 壳层状态 | 颜色切换保留风格、系统明暗变化、材质状态不改变脏状态、关闭时清理订阅 |
| 编辑稳定性 | 仅风格变化不刷新 HTML、不重建浏览器、不影响多文档状态 |

### 6.2 真实窗口与人工检查

- 九种偏好组合逐项操作；分别保存六种实际浅／深 × 标准／Mica／Acrylic 的对照截图。
- 对三种风格分别验证跟随系统的浅→深、深→浅切换。
- 标准↔Mica、标准↔Acrylic、Mica↔Acrylic，重复切换及重启恢复。
- 明亮／深色／高饱和壁纸与窗口后方动态内容：区分 Mica 与 Acrylic，检查文字可读性。
- Windows 11 原生效果；Windows 10、macOS、Linux 的实际候选和安全回退。
- 高对比度、系统透明开关、不可用合成后端；条件允许时覆盖远程桌面和软件渲染。
- 最小窗口宽度 800 DIP、常用 DPI／多屏、最大化／还原、失焦与真实鼠标拖动。
- 菜单、命令面板、对话框、BusyOverlay 与 WebView 区域的叠放、焦点和交互。

现有桌面测试基于控件 API 和路由事件，截图也不是像素基线。扩展 `StarBlogPublisher.DesktopTests/WorkspaceScenarios.cs` 时应记录实际材质结果，但原生背景效果、系统拖动和跨平台差异仍需真实桌面人工确认；WebView 内容不能仅靠 Avalonia 截图判断。

### 6.3 验证命令

```powershell
# 构建与无窗口测试
dotnet build StarBlogPublisher.sln -c Release
dotnet test StarBlogPublisher.Tests/StarBlogPublisher.Tests.csproj -c Release

# Windows 可交互桌面；采用测试项目的隔离配置
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --settings
dotnet run --project StarBlogPublisher.DesktopTests -c Release
dotnet run --project StarBlogPublisher.DesktopTests -c Release -- --webview
```

新增场景时评估现有阶段／进程超时预算。托管 CI 负责构建和单元测试，不自动宣称原生材质通过；支持平台的真实桌面检查另行记录。以上为后续实施验证命令，本计划创建时未执行。

## 7. 风险与控制

| 风险 | 控制措施 |
| --- | --- |
| 多层实色背景遮住材质 | 先验证外壳，使用语义画刷逐层放开，不一次性全局透明化 |
| 不支持材质时得到裸透明 | 检查实际结果，显式不透明回退，重点覆盖深色 |
| 切回标准仍保留原生效果 | 阶段 0 设置阻塞门槛，验证关闭请求与原生视觉结果 |
| 深色文字或语法高亮对比不足 | 正文稳定底色，状态色和焦点单独验收 |
| WebView 与浮层叠放差异 | 不做浏览器透明；真实 WebView2／平台浏览器回归 |
| 配置快照或草稿漏掉新字段 | 配置、JSON、备份、fingerprint、恢复路径逐项测试 |
| 材质变化导致编辑状态丢失 | 不重建文档／浏览器；区分颜色变化与材质变化 |
| 第三方模板升级后覆盖失效 | 优先语义资源和最小作用域覆盖，记录依赖版本 |
| 系统限制与用户预期不一致 | 保留用户偏好，显示真实效果／降级说明，不承诺全平台一致 |

## 8. 完成标准

- [ ] 设置严格采用三种颜色模式 × 三种界面风格，互不重置。
- [ ] 旧用户默认标准；配置保存、旧数据兼容、备份和草稿生命周期完整。
- [ ] Windows 11 支持环境下 Mica／Acrylic 分别可见、可区分、可切换，标准无残留。
- [ ] 不支持／禁用材质时可读、安全、实色回退，不误改用户偏好。
- [ ] 写作区、预览与颜色敏感画布清晰稳定，既有功能与交互不变。
- [ ] 自动化测试通过，真实桌面结果和未验证平台有明确记录。
- [ ] 文档说明与实际行为一致；阶段 0–3 和阶段 4–5 可分别交付。

## 9. 参考资料

- [FluentAvalonia 3.1 官方示例：FAAppWindow 应用 Mica](https://github.com/amwx/FluentAvalonia/blob/215ee0481e8b0e6d396cbe2a0f33dc791c5646ab/samples/FAControlsGallery/Views/MainWindow.axaml.cs)
- [FluentAvalonia 3.1：AppWindow 模板与默认背景](https://github.com/amwx/FluentAvalonia/blob/215ee0481e8b0e6d396cbe2a0f33dc791c5646ab/src/FluentAvalonia/Styling/ControlThemes/FAControls/AppWindowStyles.axaml)
- [Avalonia 12.1.2：WindowTransparencyLevel 定义](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.Controls/WindowTransparencyLevel.cs)
- [Avalonia 12.1.2：Windows 材质请求与能力判断](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Windows/Avalonia.Win32/WindowImpl.cs)
- [Avalonia 12.1.2：X11 透明与 KWin 模糊支持](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.X11/TransparencyHelper.cs)
- [项目测试指南](testing.md)
- [文章工作区说明](article-workspace.md)
