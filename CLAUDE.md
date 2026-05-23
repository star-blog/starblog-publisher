# StarBlog Publisher 项目指南

## 项目概述
StarBlog Publisher 是一个跨平台博客发布系统（C# .NET），包含三个端：
1. **GUI** — Avalonia UI 桌面应用（主入口）
2. **CLI** — 命令行工具，面向脚本和自动化
3. **MCP Server** — 面向 AI Agent 的标准化工具接口

三端共享同一套 Application 业务逻辑，通过 `StarBlogPublisher.Core` 类库复用。

## 项目结构

```
StarBlogPublisher.Core/       # 共享核心库（无 UI 依赖）
├── Models/                   # 数据模型（BlogPost, Category, AIProfile 等）
├── Models/Dtos/              # DTO 数据传输对象
├── Services/                 # 基础设施服务
│   ├── AppSettings.cs        # 配置管理
│   ├── GlobalState.cs        # 认证状态
│   ├── ApiService.cs         # StarBlog API (Refit)
│   ├── AIService.cs          # AI 大模型服务
│   ├── MarkdownProcessor.cs  # Markdown 处理
│   ├── ImageCompressionService.cs
│   ├── StarBlogApi/          # Refit 接口定义
│   └── Security/             # 加密服务
├── Services/Application/     # 应用服务（业务编排）
│   ├── AuthApplicationService.cs
│   ├── CategoryApplicationService.cs
│   ├── ArticlePublishApplicationService.cs
│   └── AiApplicationService.cs
└── Utils/                    # PromptBuilder, PromptTemplates 等

StarBlogPublisher/            # GUI 项目（Avalonia）
├── Models/AvaloniaImageInfo.cs  # GUI 专属展示模型
├── ViewModels/               # ViewModel 层（薄壳，调用 Application 服务）
├── Views/                    # 视图层 (.axaml)
└── Assets/

StarBlogPublisher.Cli/        # CLI + MCP Server
├── Program.cs                # 入口路由（CLI 模式 / MCP 模式）
├── McpServer.cs              # MCP Server 入口（stdio 传输）
├── Commands/                 # System.CommandLine 命令
│   ├── AuthCommand.cs        # auth login/status/logout
│   ├── CategoryCommand.cs    # category list/create
│   ├── PostCommand.cs        # post publish/get
│   └── AiCommand.cs          # ai generate-summary/optimize-title/suggest-tags/generate-slug
└── Tools/                    # MCP Tools
    ├── AuthTools.cs
    ├── CategoryTools.cs
    ├── PostTools.cs
    └── AiTools.cs
```

## 架构原则

1. **ViewModel 不直接编排业务流程** — 业务逻辑在 Application 服务中
2. **CLI 命令和 MCP Tool 只调用 Application 层 Use Case**
3. **所有日志统一通过 ILogger** — Core 不直接写 stdout
4. **MCP 模式下 stdout 仅输出协议消息** — 日志通过 stderr 输出

## 常见命令

```bash
# 构建整个解决方案
dotnet build StarBlogPublisher.sln

# 运行 GUI
dotnet run --project StarBlogPublisher

# CLI 使用
dotnet run --project StarBlogPublisher.Cli -- auth status
dotnet run --project StarBlogPublisher.Cli -- category list
dotnet run --project StarBlogPublisher.Cli -- post publish ./hello.md --category 1
dotnet run --project StarBlogPublisher.Cli -- ai generate-summary ./hello.md

# MCP Server（供 AI Agent 调用）
dotnet run --project StarBlogPublisher.Cli -- mcp
```

## MCP 配置示例

在 Claude Desktop 或 Cursor 的 MCP 配置中添加：

```json
{
  "mcpServers": {
    "starblog": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/StarBlogPublisher.Cli", "--", "mcp"]
    }
  }
}
```

### MCP Tools 列表

| Tool | 描述 |
|------|------|
| `auth_login` | 登录到 StarBlog 后端 |
| `auth_status` | 查看登录状态 |
| `auth_logout` | 登出 |
| `category_list` | 列出所有分类 |
| `category_create` | 创建新分类 |
| `post_publish` | 发布 Markdown 文件为文章 |
| `post_get` | 获取文章详情 |
| `ai_optimize_title` | AI 优化标题 |
| `ai_generate_summary` | AI 生成摘要 |
| `ai_suggest_tags` | AI 推荐标签 |
| `ai_generate_slug` | AI 生成 URL slug |
| `ai_generate_cover_prompt` | AI 生成封面图提示词 |

## 关键依赖
- **框架**: .NET 10.0
- **GUI**: Avalonia 11.3.10, CommunityToolkit.Mvvm 8.4.0
- **CLI**: System.CommandLine 2.0.8
- **MCP**: ModelContextProtocol 1.3.0
- **HTTP**: Refit 9.0.2
- **AI**: Microsoft.Extensions.AI.OpenAI
- **Markdown**: Markdig 0.44.0
- **图片处理**: SixLabors.ImageSharp 3.1.12
- **加密**: System.Security.Cryptography.ProtectedData 10.0.1

## 编码规范
- 命名空间保持一致：`StarBlogPublisher.Models`, `StarBlogPublisher.Services`, `StarBlogPublisher.Services.Application`
- 语言版本：C# default（latest minor）
- Nullable：enable
- GUI 使用 Avalonia Compiled Bindings
- MVVM 模式：CommunityToolkit.Mvvm [RelayCommand] + [ObservableProperty]

## 版本发布
- CI/CD 触发格式：`v*.*.*` 标签
- 编译目标：win-x64, linux-x64, osx-x64
- 当前框架目标为 net10.0
