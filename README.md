# StarBlog Publisher

![Avalonia](https://img.shields.io/badge/UI-Avalonia-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/License-Apache%202.0-green)
![CLI](https://img.shields.io/badge/CLI-Supported-brightgreen)
![MCP](https://img.shields.io/badge/MCP-Server-orange)

StarBlog Publisher 是一款专为 [StarBlog博客系统](https://github.com/Deali-Axy/StarBlog) 设计的专业文章发布工具。支持三种使用方式：**桌面 GUI**、**命令行 CLI** 和 **MCP Server**（供 AI Agent 调用）。

核心亮点：

* **三端共享架构**：GUI / CLI / MCP 共享同一套业务逻辑（Core 类库），行为一致
* **Markdown 文章即写即发**：编辑、预览和发布一气呵成
* **CLI 命令行工具**：支持脚本化、自动化的博客发布流程
* **MCP Server**：让 Claude、Cursor、Copilot 等 AI Agent 直接操作你的博客
* **AI 智能创作助手**：内置 OpenAI、Claude、Gemini、DeepSeek 等主流大模型
* **全平台兼容**：基于 .NET 10.0，支持 Windows、macOS 和 Linux


## 项目结构

```
StarBlogPublisher.Core/       # 共享核心库（无 UI 依赖）
├── Models/                   # 数据模型
├── Services/                 # 基础设施服务（API、AI、配置等）
├── Services/Application/     # 应用服务（业务编排层）
└── Utils/                    # PromptBuilder、PromptTemplates 等

StarBlogPublisher/            # GUI 项目（Avalonia 桌面应用）
├── ViewModels/               # ViewModel 层（调用 Application 服务）
├── Views/                    # 视图层 (.axaml)
└── Models/                   # GUI 专属模型（如 AvaloniaImageInfo）

StarBlogPublisher.Cli/        # CLI + MCP Server
├── Program.cs                # 入口路由（CLI 模式 / MCP 模式）
├── McpServer.cs              # MCP Server（stdio 传输）
├── Commands/                 # CLI 命令
└── Tools/                    # MCP Tools

StarBlogPublisher.Tests/      # 单元测试（xunit + Moq）
```

## 界面预览

### 主界面

| 主界面（浅色模式）                                  | 主界面（深色模式）                                  |
| --------------------------------------------------- | --------------------------------------------------- |
| ![主界面-浅色模式](docs/images/主界面-浅色模式.jpg) | ![主界面-深色模式](docs/images/主界面-深色模式.jpg) |

### 设置界面

| 主设置                                | AI设置                              |
| ------------------------------------- | ----------------------------------- |
| ![设置界面](docs/images/设置界面.jpg) | ![设置界面](docs/images/AI设置.jpg) |

### 其他功能

| 分类词云                              | 关于                              |
| ------------------------------------- | --------------------------------- |
| ![分类词云](docs/images/分类词云.jpg) | ![分类词云](docs/images/关于.jpg) |

## 安装与使用

### GUI 安装

**Scoop（Windows）：**

```powershell
scoop bucket add starblog-publisher https://github.com/star-blog/starblog-publisher.git
scoop install starblog-publisher/starblog-publisher
```

**手动安装：**

从 [Releases](https://github.com/star-blog/starblog-publisher/releases) 页面下载最新版本（`StarBlogPublisher-*.zip` / `StarBlogPublisher-*.tar.gz`），解压后运行。

```bash
# 或从源码运行
dotnet run --project StarBlogPublisher
```

首次运行点击设置按钮配置博客后端 API 地址，如需 AI 功能请配置 AI 提供商和 API 密钥。

### CLI 安装

CLI 工具支持多种安装方式，命令名为 `starblog`。

**Homebrew（macOS / Linux）：**

```bash
brew tap star-blog/tap
brew install starblog
```

**Scoop（Windows）：**

```powershell
scoop bucket add starblog https://github.com/star-blog/scoop-bucket.git
scoop install starblog
```

**.NET Global Tool（需要 .NET 10.0 运行时）：**

```bash
dotnet tool install --global StarBlogPublisher.Cli
```

**手动安装：**

从 [Releases](https://github.com/star-blog/starblog-publisher/releases) 页面下载对应平台的 CLI 二进制文件（`StarBlogCli-*.zip` / `StarBlogCli-*.tar.gz`），解压后将可执行文件加入 PATH。

### 发布与打包

本项目的正式发布版本会采用自包含打包，CLI 还会使用 AOT 发布来提升启动速度和运行时稳定性。

- **GUI**：AOT + 自包含发布，适合直接下载安装到本地使用。
- **CLI**：AOT + 自包含单文件发布，适合命令行工具分发和自动化场景。

如果你需要从源码自行打包，可以参考下面的命令：

```bash
# GUI AOT 发布
dotnet publish ./StarBlogPublisher/StarBlogPublisher.csproj -c Release -r osx-arm64 --self-contained true /p:PublishAot=true /p:TrimMode=full

# CLI AOT 发布
dotnet publish ./StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj -c Release -r osx-arm64 --self-contained true /p:PublishAot=true /p:TrimMode=full
```

### CLI 使用

```bash
# 认证
starblog auth login                          # 复用已保存凭据；未配置时进入交互输入
starblog auth login --username admin --password 123456
starblog auth login --username admin --password 123456 --no-prompt
starblog auth status
starblog auth logout
starblog auth logout --clear-credentials

# 分类管理
starblog category list
starblog category create --name "技术笔记"

# 文章发布
starblog post publish ./hello.md --category 1
starblog post publish ./hello.md --category 1 --draft
starblog post publish ./hello.md --category 1 --auto       # AI 自动生成标题/摘要/Slug，交互确认后发布
starblog post publish ./hello.md --category 1 --auto -y    # 自动挡 + 跳过确认直接发布
starblog post get <article-id>

# AI 辅助
starblog ai generate-summary ./hello.md
starblog ai optimize-title "原始标题"
starblog ai suggest-tags ./hello.md
starblog ai generate-slug "文章标题"

# 安装到 AI Agent（不传 --agent 时会交互选择）
starblog install skills
starblog install skills --agent claude-code
starblog install skills --agent codex
starblog install skills --agent openclaw

starblog install mcp
starblog install mcp --agent claude-code
starblog install mcp --agent codex
starblog install mcp --agent claude-code --command starblog --args mcp
```

### AI Agent 安装

`starblog install` 用于把 StarBlog Publisher 的 skill 或 MCP 配置安装到常见 AI Agent 的用户目录，默认会进入交互式选择。

当前支持：

- `skills`：Claude Code、Codex、OpenClaw
- `mcp`：Claude Code、Codex

默认安装位置：

- Claude Code skill：`~/.claude/skills/starblog-publisher/SKILL.md`
- Codex skill：`~/.agents/skills/starblog-publisher/SKILL.md`
- OpenClaw skill：`~/.openclaw/skills/starblog-publisher/SKILL.md`
- Claude Code MCP：`~/.claude.json`
- Codex MCP：`~/.codex/config.toml`

说明：

- `mcp` 默认注册命令为 `starblog mcp`，适用于已把 CLI 加入 PATH 的安装方式
- 如果你使用的是自定义可执行路径，可通过 `--command` 和 `--args` 覆盖
- OpenClaw 当前仅集成了 skill 安装，因为其公开文档没有提供稳定的通用 MCP 客户端配置契约

### MCP Server

MCP Server 模式让 AI Agent（Claude Desktop、Cursor 等）可以直接操作你的博客。

**启动 MCP Server：**

```bash
starblog mcp
```

**在 Claude Desktop / Cursor 中配置：**

```json
{
  "mcpServers": {
    "starblog": {
      "command": "starblog",
      "args": ["mcp"]
    }
  }
}
```

> 如果使用 `dotnet tool install` 安装，MCP 配置中 command 改为 `"dotnet"`，args 改为 `["tool", "run", "starblog", "mcp"]`。

**可用的 MCP Tools：**

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

## 功能特点

- **Markdown 支持**：完整支持 Markdown 格式，包括图片、链接、代码块等
- **图片上传**：自动处理 Markdown 中的本地图片，上传至 StarBlog 服务实例
- **文章预览**：实时预览 Markdown 渲染效果
- **文章管理**：支持文章的创建、编辑、发布和删除
- **分类管理**：支持按树状图显示文章分类，并支持添加分类
- **AI 辅助**：集成多种 AI 模型，提供标题润色、内容总结、关键词提取、Slug 自动生成
- **AI 自动挡发布**：`--auto` 模式一键生成标题/摘要/Slug，交互确认后发布，支持 `-y` 跳过确认
- **词云生成**：可视化展示博客内容关键词
- **主题切换**：支持亮色/暗色主题切换
- **代理设置**：支持配置 HTTP 代理
- **CLI 自动化**：命令行工具支持脚本化发布流程
- **MCP 集成**：AI Agent 可通过 MCP 协议直接操作博客

## 技术栈

- **框架**：.NET 10.0
- **GUI**：Avalonia 11.3.10 + CommunityToolkit.Mvvm 8.4.0
- **CLI**：System.CommandLine 2.0.8
- **MCP**：ModelContextProtocol 1.3.0
- **HTTP**：Refit 9.0.2
- **AI**：Microsoft.Extensions.AI.OpenAI
- **Markdown**：Markdig 0.44.0
- **图片处理**：SixLabors.ImageSharp 3.1.12
- **JSON**：Newtonsoft.Json 13.0.4
- **加密**：System.Security.Cryptography.ProtectedData 10.0.1
- **测试**：xunit + Moq + FluentAssertions

## 开发指南

### 环境准备

- .NET 10.0 SDK
- Visual Studio 2022 / Rider / VS Code

### 构建

```bash
# 构建整个解决方案
dotnet build StarBlogPublisher.sln

# 运行测试
dotnet test StarBlogPublisher.Tests/StarBlogPublisher.Tests.csproj

# 运行 GUI
dotnet run --project StarBlogPublisher

# 运行 CLI
dotnet run --project StarBlogPublisher.Cli -- --help

# 运行 MCP Server
dotnet run --project StarBlogPublisher.Cli -- mcp
```

## 贡献指南

欢迎贡献代码、报告问题或提出新功能建议！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/amazing-feature`)
3. 提交更改 (`git commit -m 'Add some amazing feature'`)
4. 推送到分支 (`git push origin feature/amazing-feature`)
5. 打开 Pull Request

## 许可证

本项目采用 Apache License 2.0 - 详情请参阅 [LICENSE](LICENSE) 文件

## 联系方式

- 项目作者：[Deali-Axy](https://github.com/Deali-Axy)
- 电子邮件：dealiaxy@gmail.com
- 项目主页：[StarBlog Publisher](https://github.com/star-blog/starblog-publisher)
- 配套博客系统：[StarBlog](https://github.com/Deali-Axy/StarBlog)

## 更新记录

### 2.0

* **重大重构**：提取 Core 共享库，三端（GUI / CLI / MCP）共享同一套业务逻辑
* **新增 CLI 命令行工具**：支持 auth、category、post、ai 等命令，可脚本化发布流程
* **新增 MCP Server**：让 AI Agent（Claude Desktop、Cursor 等）直接操作博客
* **架构优化**：从 ViewModel 中提取 Application 服务层，业务逻辑与 UI 解耦
* **新增单元测试**：71 个测试用例，覆盖核心业务逻辑
* 升级至 .NET 10.0

### 1.5

* 新增 AI 设置窗口，支持 AI 服务的初始化与配置
* 新增文章 Slug 生成功能
* 支持显示和切换多种 AI 服务模型

### 1.4

- 重构词云生成逻辑并添加加载指示器
- 添加 GitHub Actions 发布工作流和构建脚本

### 1.3

- 添加分类功能，可直接在发布工具里快速添加分类

### 1.2

- 更新 Avalonia 到 11.2.6 版本
- 预览窗口引入双栏布局

### 1.1

- 优化对 AOT 的支持

### 1.0

- 第一个发布的版本

> 完整的开发日志请参阅 [Development Log](docs/development-log.md)。

---

**StarBlog Publisher** - 为 StarBlog 打造的专业发布工具，让博客发布变得简单高效！
