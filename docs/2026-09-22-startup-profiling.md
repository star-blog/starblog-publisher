# 启动速度：测量与优化

对象是 release、framework-dependent、single-file 的 `dist\StarBlogPublisher.exe`（`build.cs` 里 `PublishSingleFile=true` 且 `IncludeNativeLibrariesForSelfExtract=true`，`--self-contained false`）。先加阶段日志并量化，再按数字改行为。不要把 Native AOT 当作本轮方案。

体验上的「启动」是：双击到主窗口内容可交互。拆成两段：

1. 进程入口到主窗口第一帧。
2. 第一帧之后，WebView2 预览和自动登录把界面填满。

## 现状（改之前）

| 位置 | 启动时做的事 |
|---|---|
| `StarBlogPublisher/Program.cs` `BuildAvaloniaApp` | 注册 FontAwesome；`UsePlatformDetect` + `WithInterFont` + `LogToTrace`。Windows 不用 Inter 做默认字体，但仍注册。 |
| `StarBlogPublisher/App.axaml.cs` `Initialize` | `AvaloniaXamlLoader.Load`，含 FluentAvalonia 主题。 |
| `App.OnFrameworkInitializationCompleted` | `RefitTypeRegistration.RegisterTypes`；`AppSettings.Instance`；`AppThemeService.Apply`；`new MainWindow` + `new MainWindowViewModel`。 |
| `ViewModels/MainWindowViewModel.cs` 构造函数 | 一次建完工作区、公众号、设置、关于。有凭据时 `_ = Login()`。 |
| `ViewModels/ArticleWorkspaceViewModel.cs` 构造函数 | 读 `workspace.json`，立刻 `new PublishViewModel`（空文档）。 |
| `Views/Controls/MarkdownEditorView.axaml` | 第一屏就有 `NativeWebView PreviewBrowser`，`Source="{Binding PreviewUri}"`。旁边已有 `dist\StarBlogPublisher.exe.WebView2\`。 |
| `ViewModels/SettingsViewModel.cs` 构造函数 | `Reload()`，并 `new AIModelCatalogService()`。用户还没打开设置。 |
| `Services/AppHttpClients.cs` | `WeChatViewModel` 要 `AppHttpClients.Factory`，第一帧前建好 `IHttpClientFactory`。 |
| `StarBlogPublisher/StarBlogPublisher.csproj` | 桌面工程引用了 `Avalonia.Browser`。若 trace 里它出现在启动栈，再从桌面 csproj 移除。 |
| `StarBlogPublisher.Core/Services/RefitTypeRegistration.cs` | 只设 `JsonConvert.DefaultSettings` 并触摸少量类型，预期不是主因。 |

## 阶段 0：只加日志，不改行为

新增一个小的启动计时器（例如 `StarBlogPublisher/Services/StartupLog.cs`），用 `Stopwatch` 从 `Program.Main` 第一行起算。每个标记一行，追加写入：

`%LOCALAPPDATA%\StarBlogPublisher\startup.log`

格式固定，方便 diff：

```text
2026-09-22T12:00:00.000Z  pid=1234  cold=true   phase=main_enter                 ms=0
2026-09-22T12:00:00.000Z  pid=1234  cold=true   phase=avalonia_builder_ready    ms=40
...
```

`cold=true` 表示本次进程启动时 `%TEMP%\.net\` 下还没有本应用的解压目录（目录名以程序集名匹配，写代码时先确认实际文件夹）。第一次标记前判断，不要在中途再判断。

必须打的点：

| phase | 位置 |
|---|---|
| `main_enter` | `Program.Main` 第一行 |
| `avalonia_builder_ready` | `BuildAvaloniaApp` 返回前 |
| `xaml_loaded` | `App.Initialize` 末尾 |
| `before_shell` | `new MainWindow` 之前 |
| `after_shell` | `MainWindow` 和 `DataContext` 赋值之后 |
| `window_opened` | `MainWindow.Opened` |
| `webview_navigation_completed` | 预览 `NativeWebView` 第一次导航完成。多个文档只记第一次。 |

日志失败必须吞掉，不能影响启动。Release 也写这份日志；本阶段不要用 `LogToTrace` 代替它。

验收：用现有 `dist\StarBlogPublisher.exe` 跑。先删 `%TEMP%\.net\` 里该应用目录再开一次（冷），保留再开一次（热）。每档各三次，丢掉每档第一次。把日志贴进 PR / 提交说明。没有这组数字就不要做阶段 1 之后的行为改动。

可选对照（不写进产品代码）：窗口出来后立刻停。

```powershell
dotnet-trace collect --profile startup -- .\dist\StarBlogPublisher.exe
```

在 trace 里搜 `AvaloniaXamlLoader`、`Skia`、`WebView`、`SettingsViewModel`、`JsonConvert`。原生解压和 WebView2 进程不在托管栈里，以阶段日志为准。

## 阶段 1：按日志改，默认顺序

只有某一阶段的毫秒数明显偏大时才做对应项。下面是预期收益从高到低的默认顺序；若日志推翻顺序，按日志改。

### 1. WebView2 退出第一帧

`MarkdownEditorView` 里的 `NativeWebView` 不要在控件加载时就创建。预览列先放占位（纯色或「预览加载中」文本）。`MainWindow.Opened` 之后，或用户第一次让预览列可见时，再创建 `NativeWebView` 并绑定 `PreviewUri`。

约束：

- 现有预览同步、主题切换后刷新 HTML、多文档切换不能回归。`MarkdownEditorView.axaml.cs` 里已有「只滚动当前文档」的注释，延后创建要沿用这个边界。
- 只记第一次 `webview_navigation_completed`。
- 不要改 WebView2 用户数据目录的位置，除非日志证明目录创建本身是热点。

### 2. 延后构造非当前页

壳启动只构造 `ArticleWorkspaceViewModel`。`WeChatViewModel`、`SettingsViewModel`、`AboutViewModel` 在第一次导航到该页时再 new，并缓存。

约束：

- 侧栏四项仍在，标题和图标不能依赖页面实例已存在。现在图标来自 `PageViewModelBase`，若延后构造，导航项改成静态描述，或单独的轻量导航模型。
- `SettingsViewModel.Reload()` 和 `AIModelCatalogService` 留在设置页第一次打开时，不要留在壳构造函数。
- `AppHttpClients.Factory` 留到第一次需要微信 HTTP 的时候。不要为了延后页面而在壳里提前碰 Factory。
- 工作区仍是默认页。空文档 `PublishViewModel` 可以留在工作区构造里，除非日志显示它很贵。

### 3. 自动登录移到第一帧之后

`MainWindowViewModel` 里 `if (AuthService.HasCredentials) _ = Login()` 改到 `window_opened` 之后再调度。窗口先出来。登录失败行为保持现状。

### 4. 发布配置对照，不默认改打包

不要在没数字时改 `build.cs`。阶段 0 之后若冷启动的 `main_enter` → `avalonia_builder_ready` 占大头，再做 A/B，每次只变一个开关：

- 对照包：非 single-file，原生 DLL 放在 exe 旁边（去掉 `PublishSingleFile` 和 `IncludeNativeLibrariesForSelfExtract`）。
- 单文件包：加上 `-p:PublishReadyToRun=true`（framework-dependent）。用体积换 JIT。

`IncludeNativeLibrariesForSelfExtract` 是体积和启动时间的交换。保留单文件分发时，把 A/B 毫秒数写进提交说明再决定是否关掉解压。

### 5. 小项

- Windows 上不要调用 `WithInterFont()`。非 Windows 保持现在的 Inter fallback。
- 发布构建去掉 `LogToTrace()`。调试构建可以留。
- 若启动 trace 出现 `Avalonia.Browser`，从 `StarBlogPublisher.csproj` 去掉该 PackageReference。先确认设计器或预览没有用它。

## 明确不做

- 不启用 Native AOT，不打开 csproj 里注释掉的 `PublishAot`。
- 不改 `RefitTypeRegistration`，除非 trace 证明它在热路径上。
- 不把设置、登录、WebView 的失败变成启动失败。
- 不为了启动去裁功能或改默认页。

## 交给实现时的完成标准

1. 阶段 0 的冷/热日志在提交说明里。
2. 行为改动每项对应日志里的一个偏大阶段。
3. 改完用同一台机器、同一个 `dist\StarBlogPublisher.exe` 发布方式再跑冷/热各三次。`window_opened` 和 `webview_navigation_completed` 的中位数应下降；没有下降的改动撤回。
4. 手工点一遍：启动即见文章页；打开一篇本地文稿后预览出现；切换浅色/深色后预览 HTML 更新；切到公众号、设置、关于再切回，状态还在；有凭据时窗口先出、登录随后完成或失败。
5. `dotnet test` 里已有的桌面场景若覆盖预览 WebView，要跟着延后创建改等待条件，不能只加超时。
