# StarBlog Publisher

![FluentAvalonia](https://img.shields.io/badge/UI-FluentAvalonia%203.1-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/License-Apache%202.0-green)
![CLI](https://img.shields.io/badge/CLI-Supported-brightgreen)
![MCP](https://img.shields.io/badge/MCP-Server-orange)

![StarBlog Client Horizontal Logo](./docs/images/horizontal-logo.webp)

StarBlog Publisher 是专为 [StarBlog](https://github.com/Deali-Axy/StarBlog) 设计的文章发布工具。3.0 以**多文档文章工作区**为桌面主界面，同时提供 **CLI** 和 **MCP Server**，三端共用 `StarBlogPublisher.Core` 里的同一套发布与 AI 逻辑。

可以这样用它：

* 在桌面里同时打开多篇 Markdown，编辑、分栏预览，再发布到 StarBlog 或送进微信公众号草稿箱
* 用命令行把发布、分类和 AI 辅助接到脚本里
* 让 Claude、Cursor、Codex 等 Agent 通过 MCP 或 Skill 操作博客

## 3.0 里能做什么

* **文章工作区**：多标签编辑，源码 / 分栏 / 预览，大纲跳转，最近文件与上次会话恢复，命令面板和专注模式
* **发布到 StarBlog**：上传正文里的本地图片，发布后给出文章 URL 和处理后的 Markdown
* **微信公众号**：33 套排版主题，浏览器预览，复制富文本，转存正文图片并创建草稿
* **封面工作室**：在 1200×900 画布上排标题，并按公众号头条（2.35:1）和次条（1:1）的安全区预览
* **AI 辅助**：补全标题、摘要、关键词和 Slug；发布前审校；生成封面提示词。建议结果需要你确认后才会写回文章
* **全平台**：基于 .NET 10，支持 Windows、macOS 和 Linux

快捷键、保存规则和面板布局见 [文章工作区](docs/article-workspace.md)。

## 项目结构

```
StarBlogPublisher.Core/          # 共享核心库（无 UI 依赖）
├── Models/                      # 文章、分类、AI 方案、公众号账号等
├── Services/                    # API、配置、Markdown、图片处理
├── Services/AI/                 # 模型目录、元数据草稿、发布前审校
├── Services/Application/        # 登录、发布、分类、公众号、封面
└── Utils/                       # Prompt 模板

StarBlogPublisher/               # Avalonia 桌面应用
├── ViewModels/                  # 工作区、设置、公众号、封面
└── Views/                       # 页面与控件

StarBlogPublisher.Cli/           # CLI 与 MCP Server
├── Commands/                    # auth、category、post、ai、install
└── Tools/                       # MCP Tools

StarBlogPublisher.Tests/         # xUnit 测试
StarBlogPublisher.DesktopTests/  # 需要真实窗口的桌面回归
```

## 安装

### 桌面应用

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

默认包是 Native AOT。需要其他运行时模式时，把包名换成 `starblog-publisher-framework-dependent` 或 `starblog-publisher-self-contained`。framework-dependent 需要本机安装 .NET 10 Runtime；Homebrew 会带上 `dotnet@10`。

**从 Release 安装：**

在 [Releases](https://github.com/star-blog/starblog-publisher/releases) 下载 `StarBlogPublisher-<平台>-<模式>-<版本>`。平台为 `windows`、`linux`、`macOS`、`macOS-arm64`，模式为 `aot`、`framework-dependent`、`self-contained`。

```bash
dotnet run --project StarBlogPublisher
```

首次打开后进入「设置」，填写博客后端地址并登录。要用 AI、公众号或代理，在对应分区里配置。

### 命令行

命令名是 `starblog`。

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

**.NET Global Tool（需要 .NET 10 运行时）：**

```bash
dotnet tool install --global StarBlogPublisher.Cli
```

也可以从 [Releases](https://github.com/star-blog/starblog-publisher/releases) 下载 `StarBlogCli-*`，解压后把可执行文件加入 PATH。

正式发布包是自包含的。GUI 默认提供 AOT 包；CLI 使用 AOT 单文件。仓库根目录的 `build.cs` 用来在本机打 GUI 包：

```bash
dotnet build.cs
dotnet build.cs --dry-run
dotnet run --file .\build.cs -- --help
```

脚本从最新 Git tag 读取版本号，并重建 `dist/`。Windows 上编译 Native AOT 需要 Visual Studio 的「使用 C++ 的桌面开发」工作负载。可选档案为 `aot`（默认，仅当前主机 RID）、`framework-dependent` 和 `self-contained`。

## 桌面应用

侧栏有四项：**文章**、**公众号排版**、**设置**、**关于**。底部可以切换浅色 / 深色，以及登录或退出。

### 文章工作区

标题栏合并了文件、编辑、查看、文章、发布和帮助菜单。中间可以搜索已打开的文章或命令。

* `Ctrl+N` / `Ctrl+O`：新建或打开 Markdown，也可以把 `.md` 拖进窗口
* `Ctrl+S`：保存正文和文章属性。正文写回原文件；标题、摘要、关键词、Slug 和分类写到同目录的 `文章.md.starblog.json`
* `Ctrl+P`：快速打开；`Ctrl+Shift+P`：命令面板
* `Ctrl+B` / `Ctrl+Alt+B` / `Ctrl+J`：文件侧栏、文章属性、任务面板
* `Ctrl+Shift+F11`：专注模式

左侧是已打开、最近打开和大纲。右侧是发布属性，底部是当前文章的任务和发布结果。关闭未保存的文章时会询问。有发布、保存或 AI 任务在跑时，需要等任务结束再关。

发布目标可以选 StarBlog 或微信公众号。选公众号时，主按钮进入排版和草稿流程，不会直接群发。

### 发布到博客

登录后选择分类，再发布或存为草稿。成功后可以复制标题、URL 和服务器处理后的 Markdown，或在浏览器中打开文章。之后做公众号排版时，会优先用这份已发布正文，方便转存图片。

文章菜单里还可以：

* 分别优化标题、摘要、关键词、Slug，或一次「补全文章属性」
* 发布前审校，查看问题、严重程度和建议
* 分析文中图片
* 生成封面提示词，或打开封面工作室
* 查看分类词云

### 微信公众号

在「设置 → 微信公众号」填写 AppId、AppSecret，可选默认作者和排版主题。AppSecret 在本机加密保存，支持多个公众号账号。

排版窗口可以切换主题、查看 HTML、在浏览器中预览，或复制富文本到公众号编辑器。上传草稿前选择 JPG/PNG 封面；正文图片会转到微信 CDN，完成后可以复制草稿 ID。上传只创建草稿。内容图片最大 1 MB，封面图最大 2 MB。

33 套主题按卡片、深度长文、科技产品、文艺随笔、活力动态和模板布局分组，代码块保留语法高亮。

封面工作室用本地图片或随机图做背景，调整字号、颜色、对齐和位置。标题画在头条和次条都会保留的区域里，确认后可交给公众号草稿。

### 设置

设置分六区，修改保存在草稿里，保存成功后才写入本机配置。离开未保存的设置页时会询问。

| 分区 | 内容 |
| --- | --- |
| 常规 | 跟随系统 / 浅色 / 深色，以及编辑器字号、换行、行号 |
| 博客 | 后端地址、账号、超时 |
| 微信公众号 | 多账号、AppSecret、默认作者和主题 |
| AI | 多套方案、服务商、模型和在线模型目录 |
| 代理 | HTTP 代理 |
| 备份 | 导出、导入可移植 JSON |

导出的 JSON 含博客密码、AI Key 和公众号 AppSecret 的明文，请单独保管。本机 `settings.json` 里这些字段是加密的。

内置 17 个 AI 服务商，并支持自定义 OpenAI 兼容接口：OpenAI、Claude、Grok、Gemini、DeepSeek、通义千问、豆包、Kimi、智谱、MiniMax、小米 MiMo、混元、千帆、硅基流动、Mistral、GroqCloud、OpenRouter。OpenRouter 还可以查看上下文窗口和价格。

关于页可以检查 GitHub Releases。发现新版本后会打开下载页，不会自动安装。

## 命令行

```bash
starblog auth login
starblog auth login --username admin --password 123456 --no-prompt
starblog auth status
starblog auth logout --clear-credentials

starblog category list
starblog category create --name "技术笔记"

starblog post publish ./hello.md --category 1
starblog post publish ./hello.md --category 1 --draft
starblog post publish ./hello.md --category 1 --auto
starblog post publish ./hello.md --category 1 --auto -y
starblog post get <article-id>

starblog ai generate-summary ./hello.md
starblog ai optimize-title "原始标题"
starblog ai suggest-tags ./hello.md
starblog ai generate-slug "文章标题"
```

`--auto` 会生成标题、摘要和 Slug，并在终端里确认。脚本里加上 `-y` 可以跳过确认。

`starblog install` 把 Skill 或 MCP 配置写到常见 Agent 的用户目录。不传 `--agent` 时进入交互选择。

```bash
starblog install skills --agent claude-code
starblog install skills --agent codex
starblog install skills --agent openclaw

starblog install mcp --agent claude-code
starblog install mcp --agent codex
starblog install mcp --command starblog --args mcp
```

| 目标 | 默认位置 |
| --- | --- |
| Claude Code Skill | `~/.claude/skills/starblog-publisher/SKILL.md` |
| Codex Skill | `~/.agents/skills/starblog-publisher/SKILL.md` |
| OpenClaw Skill | `~/.openclaw/skills/starblog-publisher/SKILL.md` |
| Claude Code MCP | `~/.claude.json` |
| Codex MCP | `~/.codex/config.toml` |

MCP 默认注册为 `starblog mcp`。自定义可执行路径时用 `--command` 和 `--args`。OpenClaw 目前只安装 Skill。

## MCP Server

```bash
starblog mcp
```

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

用 `dotnet tool` 安装时，把 command 改为 `dotnet`，args 改为 `["tool", "run", "starblog", "mcp"]`。

| Tool | 作用 |
| --- | --- |
| `auth_login` / `auth_status` / `auth_logout` | 登录、查看状态、登出 |
| `category_list` / `category_create` | 列出或创建分类 |
| `post_publish` / `post_get` | 发布 Markdown 或读取文章 |
| `ai_optimize_title` / `ai_generate_summary` / `ai_suggest_tags` / `ai_generate_slug` | 标题、摘要、标签、Slug |
| `ai_generate_cover_prompt` | 封面图提示词 |

Agent 侧的使用顺序和边界写在 [SKILLS.md](docs/SKILLS.md)，CLI 安装 Skill 时会嵌入这份说明。

## 技术栈

* **运行时**：.NET 10.0
* **桌面**：Avalonia 12.1.2、AvaloniaEdit 12.0.0、FluentAvaloniaUI 3.1.0、WebView 12.1.0、CommunityToolkit.Mvvm 8.4.2
* **CLI / MCP**：System.CommandLine 2.0.11、ModelContextProtocol 2.2.0
* **HTTP / Markdown**：Refit 15.2.0、Markdig 1.3.2、Markdown.ColorCode 3.0.1
* **AI**：Microsoft.Extensions.AI.OpenAI 10.9.0、Microsoft.Agents.AI.Workflows 1.21.0
* **图像**：SixLabors.ImageSharp 3.1.12
* **本机机密**：System.Security.Cryptography.ProtectedData 10.0.11
* **测试**：xUnit、Moq、FluentAssertions

## 开发

需要 .NET 10 SDK。Windows 上打 AOT 包还需要 C++ 桌面开发工作负载。

```bash
dotnet build StarBlogPublisher.sln
dotnet test StarBlogPublisher.Tests/StarBlogPublisher.Tests.csproj
dotnet run --project StarBlogPublisher
dotnet run --project StarBlogPublisher.Cli -- --help
dotnet run --project StarBlogPublisher.Cli -- mcp
```

`dotnet test` 覆盖 Core、CLI 和 ViewModel，不打开窗口。真实窗口回归在 `StarBlogPublisher.DesktopTests`，需要可交互桌面，说明见 [测试指南](docs/testing.md)。发布流水线和仓库密钥见 [CI/CD 指南](docs/cicd-guide.md)。

## 贡献

1. Fork 本仓库
2. 创建分支（`git checkout -b feature/amazing-feature`）
3. 提交更改
4. 推送分支并打开 Pull Request

## 许可证

[Apache License 2.0](LICENSE)

## 联系

* 作者：[Deali-Axy](https://github.com/Deali-Axy)
* 邮箱：dealiaxy@gmail.com
* 本仓库：[star-blog/starblog-publisher](https://github.com/star-blog/starblog-publisher)
* 博客系统：[StarBlog](https://github.com/Deali-Axy/StarBlog)

## 版本

### 3.0

桌面端改为文章工作区：多文档、命令面板、大纲、会话恢复，以及和发布分开的本地保存。公众号排版扩展到 33 套主题，并加入封面工作室。AI 增加需确认的属性草稿、发布前审校和在线模型目录。设置拆成常规、博客、公众号、AI、代理和备份；外观可跟随系统。

### 2.3

公众号草稿箱、发布结果、更多 AI 服务商，以及 .NET 10 构建脚本和多种发布档案。

### 2.0

抽出 Core 库，GUI、CLI、MCP 共用业务逻辑，并补上单元测试。目标框架升级到 .NET 10。

### 1.x

1.0 为首个发布版。随后加入分类、词云、AI 设置、Slug，以及 AOT 和 GitHub Actions 发布。
