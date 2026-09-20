# StarBlog Publisher

![FluentAvalonia](https://img.shields.io/badge/UI-FluentAvalonia%203.1-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/License-Apache%202.0-green)
![CLI](https://img.shields.io/badge/CLI-Supported-brightgreen)
![MCP](https://img.shields.io/badge/MCP-Server-orange)

![StarBlog Client Horizontal Logo](./docs/images/horizontal-logo.webp)

StarBlog Publisher 是一款专为 [StarBlog博客系统](https://github.com/Deali-Axy/StarBlog) 设计的专业文章发布工具。支持三种使用方式：**桌面 GUI**、**命令行 CLI** 和 **MCP Server**（供 AI Agent 调用）。

核心亮点：

* **三端共享架构**：GUI / CLI / MCP 共享同一套业务逻辑（Core 类库），行为一致
* **Fluent 桌面体验**：基于 Avalonia 与 FluentAvalonia，提供单壳侧栏导航、系统化反馈与可访问的设置页
* **Markdown 文章即写即发**：编辑、预览和发布一气呵成
* **CLI 命令行工具**：支持脚本化、自动化的博客发布流程
* **MCP Server**：让 Claude、Cursor、Copilot 等 AI Agent 直接操作你的博客
* **AI 智能创作助手**：内置 16 个 AI 服务商，并支持自定义 OpenAI 兼容接口
* **微信公众号排版与草稿箱**：一键生成微信兼容 HTML、预览和复制富文本，并上传到公众号草稿箱
* **发布结果中心**：发布后可立即获取文章 URL 和处理后的 Markdown，方便分享与二次分发
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
scoop bucket add iugamlabs https://github.com/iugamlabs/scoop
scoop install iugamlabs/starblog-publisher
```

**Homebrew（macOS / Linux）：**

```bash
brew tap iugamlabs/tap
brew install starblog-publisher
```

默认包为 Native AOT 版本。若需要其他运行时模式，请把包名替换为
`starblog-publisher-framework-dependent` 或
`starblog-publisher-self-contained`。framework-dependent 版本需要 .NET 10
Runtime；Homebrew 会自动安装 `dotnet@10` 依赖。

**手动安装：**

从 [Releases](https://github.com/star-blog/starblog-publisher/releases) 页面下载所需平台和运行时模式的最新包（`StarBlogPublisher-<platform>-<mode>-<version>`），解压后运行。

```bash
# 或从源码运行
dotnet run --project StarBlogPublisher
```

首次运行点击设置按钮配置博客后端 API 地址；如需 AI 功能，请配置 AI 提供商和 API 密钥。

### 微信公众号排版与草稿箱

GUI 支持把当前 Markdown 转换为适合微信公众号的**内联样式 HTML**。加载文章后，点击底部工具栏的“公众号排版与草稿箱”即可使用。

1. 在“设置 → 微信公众号配置”中填写公众号 `AppId`、`AppSecret`，并可选填默认作者和排版主题。`AppSecret` 会加密保存在本机。
2. 在排版窗口选择主题，查看生成的 HTML，或在浏览器中预览；可直接复制富文本并粘贴到公众号编辑器。
3. 如需上传草稿箱，选择有效的 JPG/PNG 封面图后点击“上传草稿箱”。正文图片会自动转存到微信 CDN，完成后可复制草稿 ID。

内置四套排版主题：**报刊**、**暖色卡片**、**海洋卡片**和**科技简报**；代码块会保留语法高亮。

> 上传操作只会创建公众号草稿，**不会直接群发**。内容图片最大 1 MB，封面图最大 2 MB。

### 发布后的分享与复用

文章成功发布后会自动显示发布结果窗口，提供文章标题、URL 和发布后处理的 Markdown。你可以复制标题、URL 或 Markdown，也可以一键在浏览器中打开文章。后续打开公众号排版窗口时，应用会优先使用这份已发布的 Markdown，确保图片链接可被正确转存。

### 检查更新

在 GUI 的“关于”窗口中可点击“检查更新”。应用会查询 GitHub Releases；发现新版本时，可直接跳转到发布页面下载。该功能仅检查和引导下载，不会自动下载或安装更新。

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

如需构建 GUI 发布包，请在仓库根目录运行内置的 .NET 10 单文件构建脚本：

```bash
# 发布当前操作系统支持的 GUI AOT 包，并输出到 dist/
dotnet build.cs

# 仅显示将执行的发布命令，不清空 dist/，也不构建
dotnet build.cs --dry-run
```

脚本会从最新 Git tag 读取版本号、清理发布目录中的符号文件，并重新创建 `dist/`。它使用 .NET 10 原生的单文件应用功能，不需要安装 Python 或第三方 `dotnet-script` 工具。Windows 上构建 Native AOT 还需要安装 Visual Studio 的“使用 C++ 的桌面开发”工作负载。

构建脚本支持多种发布档案：`aot`（默认，仅当前主机 RID）、`framework-dependent`（单文件，需目标计算机安装 .NET）和 `self-contained`（Windows、Linux、macOS 的单文件包）。使用 `dotnet run --file .\build.cs -- --help` 查看全部选项；可通过 `--profile`、`--rid` 选择档案与目标平台，`--compress` 可压缩自包含单文件包。

如需分别为 GUI 或 CLI 执行自定义发布，可参考下面的命令：

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
- **AI 辅助**：预置 OpenAI、Claude、Grok、Gemini、DeepSeek、通义千问、豆包、Kimi、智谱、MiniMax、混元、千帆、硅基流动、Mistral、GroqCloud、OpenRouter 等服务商，并支持自定义 OpenAI 兼容接口；提供标题润色、内容总结、关键词提取、Slug 自动生成
- **AI 自动挡发布**：`--auto` 模式一键生成标题/摘要/Slug，交互确认后发布，支持 `-y` 跳过确认
- **微信公众号排版**：将 Markdown 转为微信兼容的内联样式 HTML，提供四套主题、浏览器预览、富文本复制和代码块语法高亮
- **公众号草稿箱**：自动转存正文图片并创建草稿，可配置默认作者、封面和排版主题；不会直接群发
- **发布结果**：成功发布后显示文章 URL 与处理后的 Markdown，支持复制和在浏览器中打开
- **更新检查**：关于窗口可检查 GitHub Releases 并跳转下载最新版本
- **词云生成**：可视化展示博客内容关键词
- **主题切换**：支持亮色/暗色主题切换
- **代理设置**：支持配置 HTTP 代理
- **CLI 自动化**：命令行工具支持脚本化发布流程
- **MCP 集成**：AI Agent 可通过 MCP 协议直接操作博客

## 技术栈

- **框架**：.NET 10.0
- **GUI**：Avalonia 12.1.2 + FluentAvaloniaUI 3.1.0 + CommunityToolkit.Mvvm 8.4.2
- **CLI**：System.CommandLine 2.0.11
- **MCP**：ModelContextProtocol 2.2.0
- **HTTP**：Refit 15.2.0
- **AI**：Microsoft.Extensions.AI.OpenAI 10.9.0
- **Markdown**：Markdig 1.3.2 + Markdown.ColorCode 3.0.1
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

# 构建 GUI 发布包（在仓库根目录运行）
dotnet build.cs
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

### Unreleased

* **检查更新**：关于窗口可查询 GitHub Releases，显示检查状态，并在发现新版本后跳转至下载页面

### 2.3.0

* **微信公众号排版与草稿箱**：支持将 Markdown 转为微信兼容的内联样式 HTML，提供四套主题、浏览器预览和富文本复制；可自动转存图片并创建公众号草稿
* **代码块高亮**：微信公众号排版中的 fenced code block 支持内联样式语法高亮
* **发布结果窗口**：文章发布后展示可分享 URL 和处理后的 Markdown，支持复制及在浏览器中打开
* **AI 服务商扩展**：新增通义千问、豆包、Kimi、MiniMax、混元、千帆、硅基流动、Mistral、GroqCloud、OpenRouter 等预置服务商，并刷新默认模型目录
* **构建打包增强**：构建脚本迁移到 .NET 10，支持可选发布档案、按 RID 构建和自包含单文件压缩
* **版本信息**：应用各处统一显示由构建版本生成的版本号

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

> 早期开发日志请参阅 [Development Log](docs/archived/development-log.md)。

---

**StarBlog Publisher** - 为 StarBlog 打造的专业发布工具，让博客发布变得简单高效！

测试命令与真实窗口回归说明见 [测试指南](docs/testing.md)。
