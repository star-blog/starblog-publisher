# Plan: Add CLI + MCP Support to StarBlog Publisher

## Goal

将当前仅支持 Avalonia GUI 的应用演进为三端共享同一套业务能力的系统：

1. GUI 继续作为桌面主入口
2. CLI 提供面向脚本和自动化的命令行入口
3. MCP Server 提供面向 AI Agent 的标准化工具接口

本次重构的核心目标不是“移动文件”，而是建立清晰、稳定、可验证的分层边界，使 GUI、CLI、MCP 共享同一套应用服务而不是复制流程。

## Non-Goals

本计划不包含以下范围：

1. 不重写现有业务规则
2. 不在第一阶段引入新的博客功能
3. 不为了抽象而过度拆分项目
4. 不依赖进程内单例来支撑 CLI 多次调用和 MCP 长连接语义

## Current Findings

### 已确认的有利条件

1. 大部分 Models、Services、Utils 不依赖 Avalonia，可复用性较高
2. 图片展示相关类型是当前最明显的 GUI 专属实现点
3. 现有 API、Markdown、AI、配置、加密逻辑已经具备被复用的基础

### 已确认的重构风险

1. 关键业务编排目前位于 ViewModel，而非 Services
2. 认证状态当前依赖进程内单例，不适合 CLI 多进程调用
3. 多处服务直接写标准输出，MCP 使用 stdio 时会破坏协议流
4. 如果直接按“抽 Core + 做薄 CLI 包装”推进，CLI 和 MCP 最终仍会依赖 GUI 时代的状态模型

## Target Architecture

建议采用四层结构，而不是单纯的 GUI/Core 二分：

1. Domain
    放纯数据模型、DTO、值对象、与 UI 无关的基础规则
2. Infrastructure
    放 Refit API、配置持久化、加密、AI Client、图片压缩、Markdown 解析等外部依赖适配
3. Application
    放用例编排和业务流程，例如登录、发布文章、刷新分类、AI 生成、图片上传等
4. Presentation / Hosts
    GUI、CLI、MCP 均作为宿主层，只负责参数绑定、结果展示和错误退出码

如果希望控制项目数量，也可以先落成两个项目：

1. StarBlogPublisher.Core
    内部包含 Domain + Infrastructure + Application 命名空间
2. StarBlogPublisher
    保留 GUI
3. StarBlogPublisher.Cli
    同时承载 CLI 和 MCP Host

关键原则：

1. ViewModel 不再直接编排业务流程
2. CLI 命令和 MCP Tool 只调用 Application 层 Use Case
3. 所有日志统一通过 ILogger，禁止 Core 直接写 stdout
4. 所有认证与配置状态均通过显式接口管理，不能隐式依赖进程内静态状态

## Design Decisions

### 1. 先抽应用服务，再抽共享项目

当前最需要被复用的不是某个单独 Service，而是以下流程：

1. 登录流程
2. 发布文章流程
3. 分类刷新流程
4. AI 文本生成流程
5. 图片分析与上传流程

这些流程现在分散在 MainWindowViewModel 中。应先把流程沉淀到 Application 层，再让 GUI、CLI、MCP 共享调用。

### 2. 去除对进程内单例语义的依赖

现有 AppSettings.Instance、GlobalState.Instance、ApiService.Instance、AiService.Instance 可以在过渡期保留，但目标应改为可注入服务：

1. ISettingsStore
2. IAuthSessionStore
3. IApiClientFactory 或 IStarBlogApi
4. IAiTextService
5. IArticlePublishService

CLI 和 MCP 无法可靠复用“本进程已经登录”的假设，因此必须明确认证持久化策略。

### 3. 认证策略必须先定下来

推荐优先采用以下方案之一：

方案 A：保存用户名密码，每次命令执行时自动登录
优点：实现简单，符合当前项目已有设置模型
缺点：每次调用多一次登录请求

方案 B：持久化 JWT 及过期信息
优点：CLI 体验更好
缺点：需要处理过期、失效、登出一致性

当前最稳妥的落地顺序是先实现方案 A，再视需要扩展到方案 B。

### 4. MCP 必须从第一天就满足协议边界

MCP 采用 stdio 时：

1. 标准输出只能承载协议消息
2. 所有诊断日志必须走 stderr 或 ILogger 输出到 stderr
3. Core 中所有 Console.WriteLine 都需要替换或封装

### 5. ImageInfo 不应通过继承硬拆行为

更好的设计是拆分为：

1. Core 中保留纯数据模型 ImageInfo
2. GUI 中新增 AvaloniaImagePreview 或 ImageGalleryItemViewModel
3. GUI 专属的 Bitmap 加载逻辑放在 GUI 层工厂或适配器中

不要让 GUI 通过继承 Core 实体来叠加展示字段，否则会继续混淆领域模型和展示模型。

## Phase Plan

## Phase 0: Stabilize Architecture Boundaries

目标：先为重构扫清结构性障碍，避免后续 CLI/MCP 建在错误抽象上。

### 0.1 建立 Application 层接口

新增面向用例的服务接口与实现，至少覆盖：

1. AuthApplicationService
2. CategoryApplicationService
3. ArticlePublishApplicationService
4. ArticleQueryApplicationService
5. AiApplicationService
6. ImageApplicationService

### 0.2 将 ViewModel 中的业务编排迁移出去

重点迁移以下流程：

1. Login
2. RefreshCategories
3. Publish
4. RegenerateDescription
5. RefineTitleWithAI
6. GenerateKeywords
7. AnalyzeImages 中的图片分析逻辑

ViewModel 迁移后只保留：

1. 命令绑定
2. 窗口交互
3. 状态显示
4. 输入输出映射

### 0.3 收敛日志接口

将 Core 里的直接控制台输出替换为：

1. ILogger<T>
2. 或最小化的 IAppLogger 抽象

要求：

1. Core 不直接写 stdout
2. CLI 普通命令可以输出业务结果到 stdout
3. MCP 模式下协议输出与日志输出严格隔离

### 0.4 验证

1. GUI 构建通过
2. GUI 登录、刷新分类、发布文章、AI 生成能力不回归

## Phase 1: Create Shared Core Project

目标：在应用服务已经稳定后，再抽离共享代码，降低迁移风险。

### 1.1 创建项目

新增 StarBlogPublisher.Core，目标框架 net10.0。

建议包含：

1. Models
2. Models/Dtos
3. Services/Application
4. Services/Infrastructure
5. Services/Security
6. Services/StarBlogApi
7. Utils 中与 UI 无关的内容

### 1.2 迁移纯领域和基础设施代码

优先迁移：

1. AIProfile
2. BlogPost
3. Category
4. LoginToken
5. TitleOptimizationTemplate
6. UploadImageResult
7. WordCloud
8. Dtos 目录
9. Refit 接口
10. 加密服务
11. MarkdownProcessor
12. ImageCompressionService
13. PromptBuilder 和 PromptTemplates

### 1.3 重构图片相关模型

在 Core 中引入纯数据 ImageInfo，仅保留：

1. FilePath
2. FileName
3. FileSize
4. Exists

如确有必要，ImagePath 也应定义为“业务需要的可序列化路径值”，而不是 GUI 专属展示 URI。

在 GUI 项目中增加专用展示模型，例如：

1. ImageGalleryItemViewModel
2. 或 AvaloniaImagePreview

其中包含：

1. Bitmap
2. file:// URI
3. GUI 展示态字段

### 1.4 将 GUI 引用切换到 Core

1. GUI 项目添加 ProjectReference
2. GUI 移除重复的 NuGet 依赖
3. GUI 删除已迁移源码
4. GUI 编译和运行验证通过

### 1.5 验证

1. dotnet build StarBlogPublisher.Core/StarBlogPublisher.Core.csproj
2. dotnet build StarBlogPublisher/StarBlogPublisher.csproj
3. dotnet run --project StarBlogPublisher/StarBlogPublisher.csproj

## Phase 2: Introduce Explicit Hosting and Dependency Injection

目标：让 GUI、CLI、MCP 都能通过统一注册方式消费 Core 服务。

### 2.1 建立服务注册入口

在 Core 中提供统一扩展，例如：

1. AddStarBlogCore
2. AddStarBlogApplication
3. AddStarBlogInfrastructure

### 2.2 配置对象化

将设置访问逐步整理为可注入配置服务，而不是业务代码直接读取静态单例。

建议抽象：

1. ISettingsService
2. ICredentialProvider
3. IBackendEndpointProvider
4. IAuthSessionProvider

### 2.3 认证会话抽象

明确区分：

1. 已保存凭据
2. 当前令牌
3. 当前登录状态
4. 自动登录策略

这样 GUI、CLI、MCP 都可以复用同一套认证行为，而不是各自拼装。

### 2.4 验证

1. GUI 仍然正常启动
2. 服务可通过 DI 解析
3. 认证状态在不同 Host 中语义一致

## Phase 3: Build CLI on Top of Application Services

目标：CLI 成为 Application 层的薄宿主，而不是新一套业务实现。

### 3.1 创建 StarBlogPublisher.Cli

依赖：

1. System.CommandLine
2. Microsoft.Extensions.Hosting
3. StarBlogPublisher.Core

### 3.2 命令设计

第一版只做高价值且可验证的命令：

1. auth login
2. auth status
3. auth logout
4. category list
5. post publish
6. post get
7. ai generate-summary

不建议第一版一次性把所有 GUI 动作全部搬到 CLI。

### 3.3 输出约定

CLI 需要明确：

1. 用户可读文本输出
2. 非零退出码
3. 错误输出到 stderr
4. 后续如有必要，再增加 json 输出模式

### 3.4 认证行为

post publish 等命令不依赖“之前在本进程里执行过 auth login”。

推荐行为：

1. 若本地已保存凭据，则按策略自动登录
2. 若未保存凭据，则命令失败并返回明确错误

### 3.5 验证

1. dotnet build StarBlogPublisher.Cli/StarBlogPublisher.Cli.csproj
2. dotnet run --project StarBlogPublisher.Cli -- auth status
3. dotnet run --project StarBlogPublisher.Cli -- category list
4. dotnet run --project StarBlogPublisher.Cli -- post publish ./sample.md --category <id>

## Phase 4: Add MCP Host on the Same Application Layer

目标：在 CLI 已验证应用服务复用能力后，再暴露 MCP Tool。

### 4.1 在 CLI 项目中加入 MCP Host

保留一个独立入口：

1. starblog-publisher mcp

MCP Host 与 CLI 共用同一套 DI 和 Application 服务注册。

### 4.2 Tool 设计原则

Tool 只暴露稳定、可组合、易描述的能力：

1. auth_status
2. category_list
3. post_publish
4. post_get
5. ai_generate_summary
6. image_upload

第一版不建议暴露过多细粒度工具，避免维护成本和权限面快速膨胀。

### 4.3 MCP 协议要求

1. stdout 仅输出协议消息
2. 所有日志输出到 stderr
3. Tool 返回结构化结果，不依赖控制台文案解析

### 4.4 验证

1. dotnet run --project StarBlogPublisher.Cli -- mcp
2. 使用 MCP Inspector 或兼容客户端验证握手
3. 验证至少一个读操作 Tool 和一个写操作 Tool

## Phase 5: Documentation and Rollout

### 5.1 更新解决方案

将新项目加入解决方案。

### 5.2 更新文档

更新以下内容：

1. CLAUDE.md
2. README.md
3. CLI 用法示例
4. MCP 配置示例
5. 架构说明和迁移说明

### 5.3 发布策略

明确以下事项：

1. GUI 与 CLI 是否一起发布
2. CLI 二进制命名
3. MCP 是否复用 CLI 可执行文件
4. Scoop 包是否需要拆分或补充说明

## Project Structure Proposal

建议的目录结构如下：

StarBlogPublisher.Core/
1. Models/
2. Models/Dtos/
3. Services/Application/
4. Services/Infrastructure/
5. Services/Security/
6. Services/StarBlogApi/
7. Extensions/
8. Utils/

StarBlogPublisher/
1. ViewModels/
2. Views/
3. Models/Gui/
4. ServiceRegistration/

StarBlogPublisher.Cli/
1. Program.cs
2. Commands/
3. Mcp/
4. Formatting/
5. Hosting/

## Implementation Rules

1. 先迁业务编排，再迁文件
2. 每个 Phase 完成后都要可独立验证
3. 不允许 CLI 和 MCP 直接依赖 GUI 类型
4. 不允许 Core 直接依赖 Avalonia
5. 不允许 Core 在 MCP 模式下写 stdout
6. 不允许新的宿主入口复制业务流程代码

## Verification Matrix

每一阶段至少覆盖以下验证：

1. Build 验证
    dotnet build StarBlogPublisher.sln
2. GUI 冒烟验证
    启动 GUI，验证登录、分类刷新、文章发布、AI 生成
3. CLI 冒烟验证
    auth status、category list、post publish
4. MCP 冒烟验证
    握手成功、列出工具、执行至少一个查询工具和一个变更工具

## Acceptance Criteria

当以下条件全部满足时，认为重构完成：

1. GUI、CLI、MCP 共用同一套 Application 用例实现
2. GUI 不再承担核心业务编排职责
3. CLI 命令不依赖进程内登录状态
4. MCP 在 stdio 下稳定工作，日志不污染协议
5. 图片展示逻辑与 Core 领域模型解耦
6. 至少有一条真实 post publish 流程在 GUI 和 CLI 中都验证通过

## Recommended Execution Order

推荐按以下顺序实施：

1. 抽离 Application 层用例
2. 收敛日志与认证抽象
3. 创建 Core 项目并迁移共享代码
4. 让 GUI 切换到 Core
5. 构建 CLI
6. 构建 MCP
7. 更新文档与发布配置

这样可以最大限度降低一次性大搬迁导致的回归风险，并确保每一步都可以独立回滚和验证。
