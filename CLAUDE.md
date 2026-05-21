# StarBlog Publisher 项目指南

## 项目概述
StarBlog Publisher 是一个基于 Avalonia UI 框架的跨平台桌面应用程序（C# .NET），用于将本地 Markdown 文章发布到 StarBlog 博客系统。支持 Windows/Linux/macOS 三平台。

## 关键依赖
- **框架**: .NET 10.0, Avalonia 11.3.10
- **MVVM**: CommunityToolkit.Mvvm 8.4.0, ReactiveUI 22.3.1
- **HTTP**: Refit 9.0.2 (声明式 REST 客户端)
- **AI**: Microsoft.Extensions.AI.OpenAI (用于 AI 辅助功能)
- **Markdown**: Markdig 0.44.0, Markdown.Avalonia 11.0.3-a1
- **图片处理**: SixLabors.ImageSharp 3.1.12
- **序列化**: Newtonsoft.Json 13.0.4
- **词云**: Sdcb.WordCloud 2.0.1
- **图标**: Projektanker.Icons.Avalonia.FontAwesome 9.6.2
- **消息弹窗**: MessageBox.Avalonia 3.3.1
- **加载动画**: LoadingIndicators.Avalonia 11.0.11.1
- **加密**: System.Security.Cryptography.ProtectedData 10.0.1

## 项目结构

```
StarBlogPublisher/
├── Models/           # 数据模型 (BlogPost, Category, AIProfile 等)
├── Models/Dtos/      # DTO 数据传输对象
├── Services/         # 服务层
│   ├── AIService.cs          # AI 大模型服务
│   ├── ApiService.cs         # StarBlog API 服务 (Refit)
│   ├── AppSettings.cs        # 应用配置管理
│   ├── GlobalState.cs        # 全局状态
│   ├── ImageCompressionService.cs  # 图片压缩
│   ├── MarkdownProcessor.cs  # Markdown 处理
│   ├── StarBlogApi/          # Refit API 接口定义
│   │   ├── IAuth.cs          # 认证接口
│   │   ├── IBlogPost.cs      # 文章接口
│   │   └── ICategory.cs      # 分类接口
│   └── Security/             # 安全相关服务
├── ViewModels/       # ViewModel 层
│   ├── MainWindowViewModel.cs
│   ├── SettingsWindowViewModel.cs
│   ├── AiSettingsWindowViewModel.cs
│   ├── PreviewWindowViewModel.cs
│   ├── WordCloudWindowViewModel.cs
│   ├── ImageGalleryWindowViewModel.cs
│   ├── AddCategoryWindowViewModel.cs
│   ├── CoverPromptWindowViewModel.cs
│   ├── AboutWindowViewModel.cs
│   └── ViewModelBase.cs
├── Views/            # 视图层 (.axaml + .axaml.cs)
│   ├── MainWindow.axaml       # 主窗口
│   ├── SettingsWindow.axaml   # 设置窗口
│   ├── AiSettingsWindow.axaml # AI 设置窗口
│   ├── PreviewWindow.axaml    # 文章预览
│   ├── WordCloudWindow.axaml  # 词云窗口
│   ├── ImageGalleryWindow.axaml # 图片管理
│   ├── AddCategoryWindow.axaml  # 添加分类
│   ├── CoverPromptWindow.axaml  # AI 封面提示词
│   └── AboutWindow.axaml      # 关于窗口
└── Assets/           # 资源文件 (logo.ico 等)
```

## 核心业务流程

### 1. 登录流程
- 用户输入 StarBlog 后端的域名和用户名密码
- 通过 `ApiService` 调用 `/auth/login` 获取 JWT Token
- Token 存储在 `GlobalState` 中，后续 Refit 请求自动附加
- **注意**: 支持自定义后端地址（AppSettings.UseCustomBackend + BackendUrl）

### 2. 文章发布流程
- 选择本地 Markdown 文件 → 解析 FrontMatter（标题、分类、标签等）
- 处理 Markdown 正文（提取/上传图片、处理链接、生成目录）
- AI 辅助功能（可选）：生成/补充文章信息、优化标题、生成封面提示词
- 手动/自动关联分类、设置发布时间等
- 调用 API 发布（POST/PUT）

### 3. 图片处理
- 扫描 Markdown 中的图片链接
- 支持本地图片自动上传到 StarBlog 服务器
- 支持图片压缩（ImageCompressionService）
- 两种图片解析模式：标准模式 / 正则模式（配置 EnableRegexImageParsing）

### 4. AI 辅助流程
- 支持 OpenAI、Azure OpenAI、Ollama 等多种提供商
- 当前选用 AI 配置文件（AiProfile），包含 Provider / Key / Model / ApiBase
- 支持标题润色、文章摘要生成、标签推荐、文章分类、封面图提示词

## 关键文件及其职责

### Services/AppSettings.cs
- 应用配置管理（JSON 文件存储在 `~/.config/StarBlogPublisher/settings.json` 或 `%APPDATA%/StarBlogPublisher/settings.json`）
- 管理代理设置、后端 URL、AI 配置、登录凭据等
- 敏感信息（密码、AI Key）通过 Security/EncryptionService 加密存储
- 支持多 AI 配置文件（AIProfiles），通过配置名切换

### Services/AIService.cs
- AI 服务封装，使用 `Microsoft.Extensions.AI.IChatClient`
- 提供对话、标题润色、生成摘要、标签推荐、分类、封面提示等功能
- 支持自动切换到当前选中的 AI 配置
- **注意**: 传入的 system prompt 在每次对话时拼接，需要确保 prompt 中信息的完整性

### Services/ApiService.cs
- Refit 生成的 HTTP 客户端，封装所有 StarBlog 后端 API 调用
- 提供 POST/PUT/GET/DELETE 文章、获取分类列表、上传图片等接口
- 自动处理 Token 刷新（通过 RefitTypeRegistration 设置 HttpMessageHandler）

### Services/GlobalState.cs
- 全局单例状态，管理当前选中的文件、文章、分类列表、登录状态
- 事件驱动：`StateChanged` 通知 UI 更新

## 版本发布
- 使用 CI/CD 自动构建（GitHub Actions），触发格式：`v*.*.*` 标签
- 默认配置为 AOT 编译，生成单文件可执行文件
- 编译目标：win-x64 (.zip), linux-x64 (.tar.gz), osx-x64 (.tar.gz)
- **注意**: AOT 相关配置在 csproj 中已注释，CI 构建时通过命令行参数启用
- 当前框架目标为 net10.0，CI 使用 .NET 8.0（需要更新）

## 常见命令
- 构建：`dotnet build StarBlogPublisher/StarBlogPublisher.csproj`
- 运行：`dotnet run --project StarBlogPublisher/StarBlogPublisher.csproj`
- 发布（非 AOT）：`dotnet publish -c Release -r <rid> --self-contained true`
- 发布（AOT）：上述命令 + `/p:PublishAot=true` 及对应的裁剪参数

## 编码规范
- 命名空间：`StarBlogPublisher.Services`, `StarBlogPublisher.ViewModels`, `StarBlogPublisher.Views`, `StarBlogPublisher.Models`
- 语言版本：C# default（latest minor）
- Nullable：enable
- 使用 Avalonia Compiled Bindings (AvaloniaUseCompiledBindingsByDefault=true)
- MVVM 模式：CommunityToolkit.Mvvm [RelayCommand] + [ObservableProperty]，部分窗口使用 ReactiveUI
